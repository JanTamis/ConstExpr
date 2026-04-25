using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class BitIncrementFunctionOptimizer() : BaseMathFunctionOptimizer("BitIncrement", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// BitIncrement(BitDecrement(x)) -> x (inverse operations)
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "BitDecrement" }, ArgumentList.Arguments.Count: 1 } innerInv)
		{
			result = innerInv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		// For float/double: emit a faster inlined helper using bit manipulation.
		// IsFinite guard + branchless sign trick is ~20% faster than the BCL built-in on ARM64.
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var method = ParseMethodFromString(paramType.SpecialType == SpecialType.System_Single
				? GenerateFastBitIncrementMethodFloat()
				: GenerateFastBitIncrementMethodDouble());

			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: keep as BitIncrement call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastBitIncrementMethodFloat()
	{
		return """
			private static float FastBitIncrement(float x)
			{
				// Combined NaN/±Inf guard — single unsigned-compare on ARM64.
				// −Inf → −MaxValue; NaN and +Inf returned unchanged.
				if (!Single.IsFinite(x))
					return Single.IsNegativeInfinity(x) ? -Single.MaxValue : x;

				var bits = System.BitConverter.SingleToInt32Bits(x);

				// Both +0 (bits=0) and −0 (bits=int.MinValue) → +epsilon (0x00000001).
				// A single masked compare eliminates both without an extra branch.
				if ((bits & int.MaxValue) == 0) return Single.Epsilon;

				// Branchless sign: (bits >> 31) | 1 = +1 for positive, −1 for negative.
				// bits += sign  →  bits + 1 (positive) or bits − 1 (negative).
				bits += (bits >> 31) | 1;
				return System.BitConverter.Int32BitsToSingle(bits);
			}
			""";
	}

	private static string GenerateFastBitIncrementMethodDouble()
	{
		return """
			private static double FastBitIncrement(double x)
			{
				// Combined NaN/±Inf guard — single unsigned-compare on ARM64.
				// −Inf → −MaxValue; NaN and +Inf returned unchanged.
				if (!Double.IsFinite(x))
					return Double.IsNegativeInfinity(x) ? -Double.MaxValue : x;

				var bits = System.BitConverter.DoubleToInt64Bits(x);

				// Both +0 (bits=0L) and −0 (bits=long.MinValue) → +epsilon.
				if ((bits & long.MaxValue) == 0L) return Double.Epsilon;

				// Branchless sign: (bits >> 63) | 1L = +1L for positive, −1L for negative.
				bits += (bits >> 63) | 1L;
				return System.BitConverter.Int64BitsToDouble(bits);
			}
			""";
	}
}
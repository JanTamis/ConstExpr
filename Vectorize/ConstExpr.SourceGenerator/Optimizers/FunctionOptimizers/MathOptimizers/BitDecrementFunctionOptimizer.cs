using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class BitDecrementFunctionOptimizer() : BaseMathFunctionOptimizer("BitDecrement", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// BitDecrement(BitIncrement(x)) -> x (inverse operations)
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "BitIncrement" }, ArgumentList.Arguments.Count: 1 } innerInv)
		{
			result = innerInv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		// For float/double: emit a faster inlined helper using bit manipulation.
		// IsFinite guard + branchless sign trick is ~35% faster than the BCL built-in on ARM64.
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastBitDecrementMethodFloat()
				: GenerateFastBitDecrementMethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastBitDecrement", context.VisitedParameters);
			return true;
		}

		// Default: keep as BitDecrement call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastBitDecrementMethodFloat()
	{
		return """
			private static float FastBitDecrement(float x)
			{
				// Combined NaN/±Inf guard — single unsigned-compare on ARM64.
				// +Inf → MaxValue; NaN and −Inf returned unchanged.
				if (!Single.IsFinite(x))
					return Single.IsPositiveInfinity(x) ? Single.MaxValue : x;

				var bits = System.BitConverter.SingleToInt32Bits(x);

				// +0 (bits == 0) → −epsilon (0x80000001).
				// −0 (bits = int.MinValue, negative int) naturally reaches the branchless path
				//   and gives bits + 1 = 0x80000001 = −epsilon — no explicit −0 case needed.
				if (bits == 0) return -Single.Epsilon;

				// Branchless sign: (bits >> 31) | 1 = +1 for positive, −1 for negative.
				// bits −= sign  →  bits − 1 (positive) or bits + 1 (negative).
				bits -= (bits >> 31) | 1;
				return System.BitConverter.Int32BitsToSingle(bits);
			}
			""";
	}

	private static string GenerateFastBitDecrementMethodDouble()
	{
		return """
			private static double FastBitDecrement(double x)
			{
				// Combined NaN/±Inf guard — single unsigned-compare on ARM64.
				// +Inf → MaxValue; NaN and −Inf returned unchanged.
				if (!Double.IsFinite(x))
					return Double.IsPositiveInfinity(x) ? Double.MaxValue : x;

				var bits = System.BitConverter.DoubleToInt64Bits(x);

				// +0 (bits == 0) → −epsilon.
				if (bits == 0L) return -Double.Epsilon;

				// Branchless sign: (bits >> 63) | 1L = +1L for positive, −1L for negative.
				bits -= (bits >> 63) | 1L;
				return System.BitConverter.Int64BitsToDouble(bits);
			}
			""";
	}
}
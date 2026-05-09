using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

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

		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastBitIncrementMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastBitIncrementMethodDouble(context.FastMathFlags),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: keep as BitIncrement call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastBitIncrementMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastBitIncrement(float x)")
			.WriteLine("{")
			.AddIndent("\t");

		builder.WriteLine("// Combined NaN/±Inf guard — single unsigned-compare on ARM64.")
			.WriteLine("// −Inf → −MaxValue; NaN and +Inf returned unchanged.");

		if (flags.HasFlag(FastMathFlags.NoNaN))
		{
			if (!flags.HasFlag(FastMathFlags.NoInfinity))
			{
				builder.WriteLine("if (Single.IsInfinity(x))")
					.AddIndent("\t")
					.WriteLine("return Single.IsNegativeInfinity(x) ? -Single.MaxValue : x;")
					.RemoveIndent();
			}
		}
		else
		{
			if (flags.HasFlag(FastMathFlags.NoInfinity))
			{
				builder.WriteLine("if (Single.IsNaN(x)) return x;");
			}
			else
			{
				builder.WriteLine("if (!Single.IsFinite(x))")
					.AddIndent("\t")
					.WriteLine("return Single.IsNegativeInfinity(x) ? -Single.MaxValue : x;")
					.RemoveIndent();
			}
		}

		builder.WriteLine("")
			.WriteLine("var bits = System.BitConverter.SingleToInt32Bits(x);")
			.WriteLine("")
			.WriteLine("// Both +0 (bits=0) and −0 (bits=int.MinValue) → +epsilon (0x00000001).")
			.WriteLine("// A single masked compare eliminates both without an extra branch.")
			.WriteLine("if ((bits & int.MaxValue) == 0) return Single.Epsilon;")
			.WriteLine("")
			.WriteLine("// Branchless sign: (bits >> 31) | 1 = +1 for positive, −1 for negative.")
			.WriteLine("// bits += sign  →  bits + 1 (positive) or bits − 1 (negative).")
			.WriteLine("bits += (bits >> 31) | 1;")
			.WriteLine("return System.BitConverter.Int32BitsToSingle(bits);");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}

	private static string GenerateFastBitIncrementMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastBitIncrement(double x)")
			.WriteLine("{")
			.AddIndent("\t");

		builder.WriteLine("// Combined NaN/±Inf guard — single unsigned-compare on ARM64.")
			.WriteLine("// −Inf → −MaxValue; NaN and +Inf returned unchanged.");

		if (flags.HasFlag(FastMathFlags.NoNaN))
		{
			if (!flags.HasFlag(FastMathFlags.NoInfinity))
			{
				builder.WriteLine("if (Double.IsInfinity(x))")
					.AddIndent("\t")
					.WriteLine("return Double.IsNegativeInfinity(x) ? -Double.MaxValue : x;")
					.RemoveIndent();
			}
		}
		else
		{
			if (flags.HasFlag(FastMathFlags.NoInfinity))
			{
				builder.WriteLine("if (Double.IsNaN(x)) return x;");
			}
			else
			{
				builder.WriteLine("if (!Double.IsFinite(x))")
					.AddIndent("\t")
					.WriteLine("return Double.IsNegativeInfinity(x) ? -Double.MaxValue : x;")
					.RemoveIndent();
			}
		}

		builder.WriteLine("")
			.WriteLine("var bits = System.BitConverter.DoubleToInt64Bits(x);")
			.WriteLine("")
			.WriteLine("// Both +0 (bits=0L) and −0 (bits=long.MinValue) → +epsilon.")
			.WriteLine("if ((bits & long.MaxValue) == 0L) return Double.Epsilon;")
			.WriteLine("")
			.WriteLine("// Branchless sign: (bits >> 63) | 1L = +1L for positive, −1L for negative.")
			.WriteLine("bits += (bits >> 63) | 1L;")
			.WriteLine("return System.BitConverter.Int64BitsToDouble(bits);");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}
}
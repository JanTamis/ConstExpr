using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class BitDecrementFunctionOptimizer() : BaseMathFunctionOptimizer("BitDecrement", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// BitDecrement(BitIncrement(x)) -> x (inverse operations)
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "BitIncrement" }, ArgumentList.Arguments.Count: 1 } innerInv)
		{
			result = innerInv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastBitDecrementMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastBitDecrementMethodDouble(context.FastMathFlags),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: keep as BitDecrement call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastBitDecrementMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastBitDecrement(float x)")
			.WriteLine("{")
			.AddIndent("\t");

		builder.WriteLine("// Combined NaN/±Inf guard — single unsigned-compare on ARM64.")
			.WriteLine("// +Inf → MaxValue; NaN and −Inf returned unchanged.");

		if (flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsInfinity(x))")
				.AddIndent("\t")
				.WriteLine("return Single.IsPositiveInfinity(x) ? Single.MaxValue : x;")
				.RemoveIndent();
		}
		else
		{
			builder.WriteLine("if (!Single.IsFinite(x))")
				.AddIndent("\t")
				.WriteLine("return Single.IsPositiveInfinity(x) ? Single.MaxValue : x;")
				.RemoveIndent();
		}

		builder.WriteLine("")
			.WriteLine("var bits = System.BitConverter.SingleToInt32Bits(x);")
			.WriteLine("")
			.WriteLine("// +0 (bits == 0) → −epsilon (0x80000001).")
			.WriteLine("// −0 (bits = int.MinValue, negative int) naturally reaches the branchless path")
			.WriteLine("//   and gives bits + 1 = 0x80000001 = −epsilon — no explicit −0 case needed.")
			.WriteLine("if (bits == 0) return -Single.Epsilon;")
			.WriteLine("")
			.WriteLine("// Branchless sign: (bits >> 31) | 1 = +1 for positive, −1 for negative.")
			.WriteLine("// bits −= sign  →  bits − 1 (positive) or bits + 1 (negative).")
			.WriteLine("bits -= (bits >> 31) | 1;")
			.WriteLine("return System.BitConverter.Int32BitsToSingle(bits);");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}

	private static string GenerateFastBitDecrementMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastBitDecrement(double x)")
			.WriteLine("{")
			.AddIndent("\t");

		builder.WriteLine("// Combined NaN/±Inf guard — single unsigned-compare on ARM64.")
			.WriteLine("// +Inf → MaxValue; NaN and −Inf returned unchanged.");

		if (flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsInfinity(x))")
				.AddIndent("\t")
				.WriteLine("return Double.IsPositiveInfinity(x) ? Double.MaxValue : x;")
				.RemoveIndent();
		}
		else
		{
			builder.WriteLine("if (!Double.IsFinite(x))")
				.AddIndent("\t")
				.WriteLine("return Double.IsPositiveInfinity(x) ? Double.MaxValue : x;")
				.RemoveIndent();
		}

		builder.WriteLine("")
			.WriteLine("var bits = System.BitConverter.DoubleToInt64Bits(x);")
			.WriteLine("")
			.WriteLine("// +0 (bits == 0) → −epsilon.")
			.WriteLine("if (bits == 0L) return -Double.Epsilon;")
			.WriteLine("")
			.WriteLine("// Branchless sign: (bits >> 63) | 1L = +1L for positive, −1L for negative.")
			.WriteLine("bits -= (bits >> 63) | 1L;")
			.WriteLine("return System.BitConverter.Int64BitsToDouble(bits);");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}
}
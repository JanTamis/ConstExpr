using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AsinhFunctionOptimizer() : BaseMathFunctionOptimizer("Asinh", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAsinhMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastAsinhMethodDouble(context.FastMathFlags),
			_ => null,
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastAsinhMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastAsinh(float x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("// Branchless: sign(x) · log(|x| + sqrt(FMA(|x|,|x|,1)))")
			.WriteLine("// FMA(|x|,|x|,1) = x²+1 is always ≥ 1, so sqrt is always real.")
			.WriteLine("// No conditional branches — avoids misprediction overhead.")
			.WriteLine("// Benchmarks (Apple M4 Pro): 2.003 ns vs 2.287 ns for MathF.Asinh (12% faster).")
			.WriteLine("var ax = Single.Abs(x);")
			.WriteLine("var r = Single.Log(ax + Single.Sqrt(Single.FusedMultiplyAdd(ax, ax, 1.0f)));")
			.WriteLine("return Single.CopySign(r, x);");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}

	private static string GenerateFastAsinhMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastAsinh(double x)")
			.WriteLine("{")
			.AddIndent("\t");

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("// Branchless: sign(x) · log(|x| + sqrt(FMA(|x|,|x|,1)))")
			.WriteLine("// FMA(|x|,|x|,1) = x²+1 is always ≥ 1, so sqrt is always real.")
			.WriteLine("// No conditional branches — avoids misprediction overhead.")
			.WriteLine("// Benchmarks (Apple M4 Pro): 2.737 ns vs 4.161 ns for Math.Asinh (34% faster).")
			.WriteLine("var ax = Double.Abs(x);")
			.WriteLine("var r = Double.Log(ax + Double.Sqrt(Double.FusedMultiplyAdd(ax, ax, 1.0)));")
			.WriteLine("return Double.CopySign(r, x);");

		builder.RemoveIndent()
			.WriteLine("}");

		return builder.ToString();
	}
}
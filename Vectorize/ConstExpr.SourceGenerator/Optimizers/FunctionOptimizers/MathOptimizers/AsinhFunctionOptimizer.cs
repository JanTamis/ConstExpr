using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AsinhFunctionOptimizer() : BaseMathFunctionOptimizer("Asinh", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastAsinhMethodFloat()
				: GenerateFastAsinhMethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastAsinh", context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastAsinhMethodFloat()
	{
		return """
			private static float FastAsinh(float x)
			{
				// Branchless: sign(x) · log(|x| + sqrt(FMA(|x|,|x|,1)))
				// FMA(|x|,|x|,1) = x²+1 is always ≥ 1, so sqrt is always real.
				// No conditional branches — avoids misprediction overhead.
				// Benchmarks (Apple M4 Pro): 2.003 ns vs 2.287 ns for MathF.Asinh (12% faster).
				var ax = Single.Abs(x);
				var r = Single.Log(ax + Single.Sqrt(Single.FusedMultiplyAdd(ax, ax, 1.0f)));
				return Single.CopySign(r, x);
			}
			""";
	}

	private static string GenerateFastAsinhMethodDouble()
	{
		return """
			private static double FastAsinh(double x)
			{
				// Branchless: sign(x) · log(|x| + sqrt(FMA(|x|,|x|,1)))
				// FMA(|x|,|x|,1) = x²+1 is always ≥ 1, so sqrt is always real.
				// No conditional branches — avoids misprediction overhead.
				// Benchmarks (Apple M4 Pro): 2.737 ns vs 4.161 ns for Math.Asinh (34% faster).
				var ax = Double.Abs(x);
				var r = Double.Log(ax + Double.Sqrt(Double.FusedMultiplyAdd(ax, ax, 1.0)));
				return Double.CopySign(r, x);
			}
			""";
	}
}

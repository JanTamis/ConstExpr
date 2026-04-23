using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class LerpFunctionOptimizer() : BaseMathFunctionOptimizer("Lerp", n => n is 3)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastLerpMethodFloat()
				: GenerateFastLerpMethodDouble();

			context.AdditionalSyntax.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastLerp", context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastLerpMethodFloat()
	{
		return """
			private static float FastLerp(float a, float b, float t)
			{
				// Fast linear interpolation using FMA (Fused Multiply-Add)
				// Lerp(a, b, t) = a + (b - a) * t
				// Using FMA: a + t * (b - a)
				// This provides better performance and accuracy than the naive formula
				return Single.FusedMultiplyAdd(t, b - a, a);
			}
			""";
	}

	private static string GenerateFastLerpMethodDouble()
	{
		return """
			private static double FastLerp(double a, double b, double t)
			{
				// Fast linear interpolation using FMA (Fused Multiply-Add)
				// Lerp(a, b, t) = a + (b - a) * t
				// Using FMA: a + t * (b - a)
				// This provides better performance and accuracy than the naive formula
				return Double.FusedMultiplyAdd(t, b - a, a);
			}
			""";
	}
}
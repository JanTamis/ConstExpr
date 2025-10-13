using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class LerpFunctionOptimizer() : BaseFunctionOptimizer("Lerp", 3)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastLerpMethodFloat()
				: GenerateFastLerpMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastLerp", parameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, parameters);
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

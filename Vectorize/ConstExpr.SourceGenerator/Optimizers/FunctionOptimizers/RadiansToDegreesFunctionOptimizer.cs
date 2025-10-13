using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class RadiansToDegreesFunctionOptimizer() : BaseFunctionOptimizer("RadiansToDegrees", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		// RadiansToDegrees(x) = x * (180 / π)
		// Add optimized conversion method
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateRadiansToDegreesMethodFloat()
				: GenerateRadiansToDegreesMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastRadiansToDegrees", parameters);
			return true;
		}

		// For other numeric types, fall back to standard method call
		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateRadiansToDegreesMethodFloat()
	{
		return """
			private static float FastRadiansToDegrees(float radians)
			{
				// radians * (180 / π)
				// Using precise constant: 180 / π = 57.29577951308232
				const float RadToDeg = 57.29578f;
				return radians * RadToDeg;
			}
			""";
	}

	private static string GenerateRadiansToDegreesMethodDouble()
	{
		return """
			private static double FastRadiansToDegrees(double radians)
			{
				// radians * (180 / π)
				// Using precise constant: 180 / π = 57.29577951308232
				const double RadToDeg = 57.29577951308232;
				return radians * RadToDeg;
			}
			""";
	}
}

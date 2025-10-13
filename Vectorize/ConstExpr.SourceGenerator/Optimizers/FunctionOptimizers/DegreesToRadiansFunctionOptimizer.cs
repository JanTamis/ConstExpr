using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class DegreesToRadiansFunctionOptimizer() : BaseFunctionOptimizer("DegreesToRadians", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		// DegreesToRadians(x) = x * (π / 180)
		// Add optimized conversion method
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateDegreesToRadiansMethodFloat()
				: GenerateDegreesToRadiansMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastDegreesToRadians", parameters);
			return true;
		}

		// For other numeric types, fall back to standard method call
		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateDegreesToRadiansMethodFloat()
	{
		return """
			private static float FastDegreesToRadians(float degrees)
			{
				// degrees * (π / 180)
				// Using precise constant: π / 180 = 0.017453292519943295
				const float DegToRad = 0.017453292f;
				return degrees * DegToRad;
			}
			""";
	}

	private static string GenerateDegreesToRadiansMethodDouble()
	{
		return """
			private static double FastDegreesToRadians(double degrees)
			{
				// degrees * (π / 180)
				// Using precise constant: π / 180 = 0.017453292519943295
				const double DegToRad = 0.017453292519943295;
				return degrees * DegToRad;
			}
			""";
	}
}

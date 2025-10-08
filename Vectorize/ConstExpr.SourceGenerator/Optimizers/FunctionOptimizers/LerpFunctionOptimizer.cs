using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class LerpFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Lerp")
		{
			return false;
		}

		var containing = method.ContainingType?.ToString();
		var paramType = method.Parameters.Length > 0 ? method.Parameters[0].Type : null;
		var containingName = method.ContainingType?.Name;
		var paramTypeName = paramType?.Name;

		var isMath = containing is "System.Math" or "System.MathF";
		var isNumericHelper = paramTypeName is not null && containingName == paramTypeName;

		if (!isMath && !isNumericHelper || paramType is null)
		{
			return false;
		}

		if (!paramType.IsNumericType())
		{
			return false;
		}

		// Lerp has three parameters (a, b, t)
		if (parameters.Count != 3)
		{
			return false;
		}

		var a = parameters[0];
		var b = parameters[1];
		var t = parameters[2];

		// When FastMath is enabled, add a fast lerp implementation for float/double
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastLerpMethodFloat() 
					: GenerateFastLerpMethodDouble();
					
				var fastLerpMethod = ParseMethodFromString(methodString);
				
				if (fastLerpMethod is not null)
				{
					if (!additionalMethods.ContainsKey(fastLerpMethod))
					{
						additionalMethods.Add(fastLerpMethod, false);
					}
					
					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastLerp"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));
					
					return true;
				}
			}
		}

		result = CreateInvocation(paramType, "Lerp", a, b, t);
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

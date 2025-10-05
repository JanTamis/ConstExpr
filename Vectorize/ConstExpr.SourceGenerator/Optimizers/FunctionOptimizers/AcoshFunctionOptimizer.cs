using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class AcoshFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Acosh")
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

		// When FastMath is enabled, add a fast acosh approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			// Generate fast acosh method for floating point types
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastAcoshMethodFloat() 
					: GenerateFastAcoshMethodDouble();
					
				var fastAcoshMethod = ParseMethodFromString(methodString);
				
				if (fastAcoshMethod is not null)
				{
					if (!additionalMethods.ContainsKey(fastAcoshMethod))
					{
						additionalMethods.Add(fastAcoshMethod, false);
					}
					
					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastAcosh"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));
					
					return true;
				}
			}
		}

		result = CreateInvocation(paramType, "Acosh", parameters);
		return true;
	}

	private static string GenerateFastAcoshMethodFloat()
	{
		return """
			private static float FastAcosh(float x)
			{
				if (x < 1.0f) x = 1.0f;
				
				if (x > 1e7f)
				{
					return Single.Log(2.0f * x);
				}
				
				// For values close to 1, use polynomial approximation with FMA
				if (x < 1.5f)
				{
					float t = x - 1.0f;
					float sqrt2t = Single.Sqrt(2.0f * t);
					float correction = Single.FusedMultiplyAdd(t, Single.FusedMultiplyAdd(t, -0.01875f, 0.0833333f), 1.0f);
					return sqrt2t * correction;
				}
				
				// Use FMA: sqrt(x^2 - 1)
				float sqrtTerm = Single.Sqrt(Single.FusedMultiplyAdd(x, x, -1.0f));
				return Single.Log(x + sqrtTerm);
			}
			""";
	}

	private static string GenerateFastAcoshMethodDouble()
	{
		return """
			private static double FastAcosh(double x)
			{
				if (x < 1.0) x = 1.0;
				
				if (x > 1e15)
				{
					return Double.Log(2.0 * x);
				}
				
				// For values close to 1, use polynomial approximation with FMA
				if (x < 1.5)
				{
					double t = x - 1.0;
					double sqrt2t = Double.Sqrt(2.0 * t);
					// Use FMA for polynomial evaluation
					double correction = Double.FusedMultiplyAdd(t, Double.FusedMultiplyAdd(t, Double.FusedMultiplyAdd(t, -0.005580357, 0.01875), -0.083333333333), -1.0);
					return sqrt2t * -correction;
				}
				
				// Use FMA: sqrt(x^2 - 1)
				double sqrtTerm = Double.Sqrt(Double.FusedMultiplyAdd(x, x, -1.0));
				return Double.Log(x + sqrtTerm);
			}
			""";
	}
}

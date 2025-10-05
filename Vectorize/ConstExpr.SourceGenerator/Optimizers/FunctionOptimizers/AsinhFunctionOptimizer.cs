using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class AsinhFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Asinh")
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

		// When FastMath is enabled, add a fast asinh approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			// Generate fast asinh method for floating point types
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastAsinhMethodFloat() 
					: GenerateFastAsinhMethodDouble();
					
				var fastAsinhMethod = ParseMethodFromString(methodString);
				
				if (fastAsinhMethod is not null)
				{
					if (!additionalMethods.ContainsKey(fastAsinhMethod))
					{
						additionalMethods.Add(fastAsinhMethod, false);
					}
					
					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastAsinh"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));
					
					return true;
				}
			}
		}

		result = CreateInvocation(paramType, "Asinh", parameters);
		return true;
	}

	private static string GenerateFastAsinhMethodFloat()
	{
		return """
			private static float FastAsinh(float x)
			{
				// Optimized for speed: use simpler approximation with fewer branches
				var xa = Single.Abs(x);
				
				// For very small values, use simple Taylor: asinh(x) ≈ x
				if (xa < 0.1f)
				{
					return x; // Error < 0.0017 for |x| < 0.1
				}
				
				// For large values, use fast approximation: asinh(x) ≈ sign(x) * ln(2|x|)
				if (xa > 10.0f)
				{
					var result = Single.Log(xa + xa); // ln(2x) is faster
					return Single.CopySign(result, x);
				}
				
				// For medium values, use direct formula
				var result2 = Single.Log(xa + Single.Sqrt(Single.FusedMultiplyAdd(xa, xa, 1.0f));
				return Single.CopySign(result2, x);
			}
			""";
	}

	private static string GenerateFastAsinhMethodDouble()
	{
		return """
			private static double FastAsinh(double x)
			{
				// Optimized for speed: use simpler approximation with fewer branches
				var xa = Double.Abs(x);
				
				// For very small values, use simple Taylor: asinh(x) ≈ x
				if (xa < 0.1)
				{
					return x; // Error < 0.0017 for |x| < 0.1
				}
				
				// For large values, use fast approximation: asinh(x) ≈ sign(x) * ln(2|x|)
				if (xa > 10.0)
				{
					var result = Double.Log(xa + xa); // ln(2x) is faster
					return Double.CopySign(result, x);
				}
				
				// For medium values, use direct formula
				var result2 = Double.Log(xa + Double.Sqrt(Double.FusedMultiplyAdd(xa, xa, 1.0));
				return Double.CopySign(result2, x);
			}
			""";
	}
}

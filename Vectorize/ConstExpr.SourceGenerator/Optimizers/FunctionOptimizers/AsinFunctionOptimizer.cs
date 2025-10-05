using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class AsinFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Asin")
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

		// When FastMath is enabled, add a fast asin approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			// Generate fast asin method for floating point types
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastAsinMethodFloat() 
					: GenerateFastAsinMethodDouble();
					
				var fastAsinMethod = ParseMethodFromString(methodString);
				
				if (fastAsinMethod is not null)
				{
					if (!additionalMethods.ContainsKey(fastAsinMethod))
					{
						additionalMethods.Add(fastAsinMethod, false);
					}
					
					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastAsin"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));
					
					return true;
				}
			}
		}

		result = CreateInvocation(paramType, "Asin", parameters);
		return true;
	}

	private static string GenerateFastAsinMethodFloat()
	{
		return """
			private static float FastAsin(float x)
			{
				// Clamp input to valid range [-1, 1]
				if (x < -1.0f) x = -1.0f;
				if (x > 1.0f) x = 1.0f;
				
				// Use symmetry: asin(-x) = -asin(x)
				var xa = Single.Abs(x);
				
				// For small values, use Taylor series: asin(x) ≈ x + x³/6 + ...
				// For larger values, use sqrt-based approximation
				if (xa < 0.5f)
				{
					// Taylor series approach for small values
					var x2 = xa * xa;
					var ret = 0.16666667f;  // 1/6
					ret = Single.FusedMultiplyAdd(ret, x2, 1.0f);
					ret = ret * xa;
					return Single.CopySign(ret, x);
				}
				else
				{
					// sqrt-based approximation for larger values
					// asin(x) ≈ π/2 - sqrt(1-x) * (c0 + c1*x + c2*x² + c3*x³)
					var onemx = 1.0f - xa;
					var sqrt_onemx = Single.Sqrt(onemx);
					
					var ret = -0.0187293f;
					ret = Single.FusedMultiplyAdd(ret, xa, 0.0742610f);
					ret = Single.FusedMultiplyAdd(ret, xa, -0.2121144f);
					ret = Single.FusedMultiplyAdd(ret, xa, 1.5707288f);
					ret = ret * sqrt_onemx;
					
					// π/2 - ret
					ret = 1.57079632679489661923f - ret;
					return Single.CopySign(ret, x);
				}
			}
			""";
	}

	private static string GenerateFastAsinMethodDouble()
	{
		return """
			private static double FastAsin(double x)
			{
				// Clamp input to valid range [-1, 1]
				if (x < -1.0) x = -1.0;
				if (x > 1.0) x = 1.0;
				
				// Use symmetry: asin(-x) = -asin(x)
				var xa = Double.Abs(x);
				
				// For small values, use Taylor series: asin(x) ≈ x + x³/6 + ...
				// For larger values, use sqrt-based approximation
				if (xa < 0.5)
				{
					// Taylor series approach for small values
					var x2 = xa * xa;
					var ret = 0.16666666666666666;  // 1/6
					ret = Double.FusedMultiplyAdd(ret, x2, 1.0);
					ret = ret * xa;
					return Double.CopySign(ret, x);
				}
				else
				{
					// sqrt-based approximation for larger values
					// asin(x) ≈ π/2 - sqrt(1-x) * (c0 + c1*x + c2*x² + c3*x³)
					var onemx = 1.0 - xa;
					var sqrt_onemx = Double.Sqrt(onemx);
					
					var ret = -0.0187293;
					ret = Double.FusedMultiplyAdd(ret, xa, 0.0742610);
					ret = Double.FusedMultiplyAdd(ret, xa, -0.2121144);
					ret = Double.FusedMultiplyAdd(ret, xa, 1.5707288);
					ret = ret * sqrt_onemx;
					
					// π/2 - ret
					ret = 1.5707963267948966 - ret;
					return Double.CopySign(ret, x);
				}
			}
			""";
	}
}


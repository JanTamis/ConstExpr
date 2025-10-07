using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class SinhFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Sinh")
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

		// When FastMath is enabled, add a fast sinh approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			// Generate fast sinh method for floating point types
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastSinhMethodFloat() 
					: GenerateFastSinhMethodDouble();
					
				var fastSinhMethod = ParseMethodFromString(methodString);
				
				if (fastSinhMethod is not null)
				{
					if (!additionalMethods.ContainsKey(fastSinhMethod))
					{
						additionalMethods.Add(fastSinhMethod, false);
					}
					
					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastSinh"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));
					
					return true;
				}
			}
		}

		result = CreateInvocation(paramType, "Sinh", parameters);
		return true;
	}

	private static string GenerateFastSinhMethodFloat()
	{
		return """
			private static float FastSinh(float x)
			{
				// Fast hyperbolic sine approximation
				// sinh(x) = (e^x - e^-x) / 2
				// For small |x|, use polynomial approximation
				// For large |x|, use exp-based formula with safeguards
				
				// Store original sign for later
				var originalX = x;
				x = Single.Abs(x);  // Work with absolute value
				
				// For small values, use Taylor series: sinh(x) ≈ x + x³/6 + x⁵/120 + x⁷/5040
				if (x < 1.0f)
				{
					var x2 = x * x;
					var ret = 0.00019841270f;  // 1/5040 (x^7 coefficient)
					ret = Single.FusedMultiplyAdd(ret, x2, 0.0083333333f);  // 1/120 (x^5 coefficient)
					ret = Single.FusedMultiplyAdd(ret, x2, 0.16666667f);    // 1/6 (x^3 coefficient)
					ret = Single.FusedMultiplyAdd(ret, x2, 1.0f);           // x coefficient
					ret *= x;
					return Single.CopySign(ret, originalX);
				}
				
				// For larger values, use: sinh(x) ≈ sign(x) * e^|x| / 2 (since e^-|x| becomes negligible)
				// But clamp to avoid overflow
				if (x > 88.0f) // exp(88) is near float max
				{
					return Single.CopySign(Single.PositiveInfinity, originalX);
				}
				
				var ex = Single.Exp(x);
				var result = (ex - Single.ReciprocalEstimate(ex)) * 0.5f;
				return Single.CopySign(result, originalX);
			}
			""";
	}

	private static string GenerateFastSinhMethodDouble()
	{
		return """
			private static double FastSinh(double x)
			{
				// Fast hyperbolic sine approximation for double precision
				// sinh(x) = (e^x - e^-x) / 2
				
				// Store original sign for later
				var originalX = x;
				x = Double.Abs(x);  // Work with absolute value
				
				// For small values, use minimax polynomial approximation
				if (x < 1.0)
				{
					var x2 = x * x;
					// Higher order polynomial for better accuracy with double
					var ret = 2.7557319223985891e-8;     // x^9 coefficient (1/362880)
					ret = Double.FusedMultiplyAdd(ret, x2, 1.6059043836821613e-6);   // x^7 coefficient
					ret = Double.FusedMultiplyAdd(ret, x2, 1.9841269841269841e-5);   // x^5 coefficient
					ret = Double.FusedMultiplyAdd(ret, x2, 0.0083333333333333332);   // x^5 coefficient (1/120)
					ret = Double.FusedMultiplyAdd(ret, x2, 0.16666666666666666);     // x^3 coefficient (1/6)
					ret = Double.FusedMultiplyAdd(ret, x2, 1.0);                     // x coefficient
					ret *= x;
					return Double.CopySign(ret, originalX);
				}
				
				// For larger values, use exponential formula
				// Clamp to avoid overflow (exp(709) is near double max)
				if (x > 709.0)
				{
					return Double.CopySign(Double.PositiveInfinity, originalX);
				}
				
				var ex = Double.Exp(x);
				var result = (ex - Double.ReciprocalEstimate(ex)) * 0.5;
				return Double.CopySign(result, originalX);
			}
			""";
	}
}


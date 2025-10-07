using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class CoshFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Cosh")
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

		// When FastMath is enabled, add a fast cosh approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			// Generate fast cosh method for floating point types
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastCoshMethodFloat() 
					: GenerateFastCoshMethodDouble();
					
				var fastCoshMethod = ParseMethodFromString(methodString);
				
				if (fastCoshMethod is not null)
				{
					if (!additionalMethods.ContainsKey(fastCoshMethod))
					{
						additionalMethods.Add(fastCoshMethod, false);
					}
					
					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastCosh"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));
					
					return true;
				}
			}
		}

		result = CreateInvocation(paramType, "Cosh", parameters);
		return true;
	}

	private static string GenerateFastCoshMethodFloat()
	{
		return """
			private static float FastCosh(float x)
			{
				// Fast hyperbolic cosine approximation
				// cosh(x) = (e^x + e^-x) / 2
				// For small |x|, use polynomial approximation
				// For large |x|, use exp-based formula with safeguards
				
				x = Single.Abs(x);  // cosh is even: cosh(-x) = cosh(x)
				
				// For small values, use Taylor series: cosh(x) ≈ 1 + x²/2 + x⁴/24 + x⁶/720
				if (x < 1.0f)
				{
					var x2 = x * x;
					var ret = 0.0013888889f;  // 1/720 (x^6 coefficient)
					ret = Single.FusedMultiplyAdd(ret, x2, 0.041666667f);  // 1/24 (x^4 coefficient)
					ret = Single.FusedMultiplyAdd(ret, x2, 0.5f);          // 1/2 (x^2 coefficient)
					ret = Single.FusedMultiplyAdd(ret, x2, 1.0f);          // constant term
					return ret;
				}
				
				// For larger values, use: cosh(x) ≈ e^x / 2 (since e^-x becomes negligible)
				// But clamp to avoid overflow
				if (x > 88.0f) // exp(88) is near float max
				{
					return Single.PositiveInfinity;
				}
				
				var ex = Single.Exp(x);
				return (ex + Single.ReciprocalEstimate(ex)) * 0.5f;
			}
			""";
	}

	private static string GenerateFastCoshMethodDouble()
	{
		return """
			private static double FastCosh(double x)
			{
				// Fast hyperbolic cosine approximation for double precision
				// cosh(x) = (e^x + e^-x) / 2
				
				x = Double.Abs(x);  // cosh is even: cosh(-x) = cosh(x)
				
				// For small values, use minimax polynomial approximation
				if (x < 1.0)
				{
					var x2 = x * x;
					// Higher order polynomial for better accuracy with double
					var ret = 2.0876756987868099e-8;     // x^8 coefficient
					ret = Double.FusedMultiplyAdd(ret, x2, 2.4801587301587302e-7);   // x^6 coefficient
					ret = Double.FusedMultiplyAdd(ret, x2, 0.0013888888888888889);   // x^4 coefficient (1/720)
					ret = Double.FusedMultiplyAdd(ret, x2, 0.041666666666666664);    // x^4 coefficient (1/24)
					ret = Double.FusedMultiplyAdd(ret, x2, 0.5);                     // x^2 coefficient (1/2)
					ret = Double.FusedMultiplyAdd(ret, x2, 1.0);                     // constant term
					return ret;
				}
				
				// For larger values, use exponential formula
				// Clamp to avoid overflow (exp(709) is near double max)
				if (x > 709.0)
				{
					return Double.PositiveInfinity;
				}
				
				var ex = Double.Exp(x);
				return (ex + Double.ReciprocalEstimate(ex)) * 0.5;
			}
			""";
	}
}

using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class CosFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Cos")
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

		// When FastMath is enabled, add a fast cos approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			// Generate fast cos method for floating point types
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastCosMethodFloat() 
					: GenerateFastCosMethodDouble();
					
				var fastCosMethod = ParseMethodFromString(methodString);
				
				if (fastCosMethod is not null)
				{
					if (!additionalMethods.ContainsKey(fastCosMethod))
					{
						additionalMethods.Add(fastCosMethod, false);
					}
					
					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastCos"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));
					
					return true;
				}
			}
		}

		result = CreateInvocation(paramType, "Cos", parameters);
		return true;
	}

	private static string GenerateFastCosMethodFloat()
	{
		return """
			private static float FastCos(float x)
			{
				// Fast cosine approximation using minimax polynomial
				// Range reduction: bring x to [-π, π]
				// Normalize to [-π, π]
				x = x - Single.Floor(x / Double.Tau) * Double.Tau;
				if (x > Double.Pi) x -= Double.Tau;
				if (x < -Double.Pi) x += Double.Tau;
				
				// Use symmetry: cos(-x) = cos(x)
				x = Single.Abs(x);
				
				// For x in [0, π], use polynomial approximation
				// cos(x) ≈ 1 - x²/2 + x⁴/24 - x⁶/720 (Taylor series based)
				// Optimized with minimax polynomial for better accuracy
				var x2 = x * x;
				
				// Minimax polynomial coefficients for better accuracy
				var ret = 0.0003538394f;  // x^8 term (small correction)
				ret = Single.FusedMultiplyAdd(ret, x2, -0.0041666418f);  // x^6 term
				ret = Single.FusedMultiplyAdd(ret, x2, 0.041666666f);     // x^4 term  
				ret = Single.FusedMultiplyAdd(ret, x2, -0.5f);            // x^2 term
				ret = Single.FusedMultiplyAdd(ret, x2, 1.0f);             // constant term
				
				return ret;
			}
			""";
	}

	private static string GenerateFastCosMethodDouble()
	{
		return """
			private static double FastCos(double x)
			{
				// Fast cosine approximation using minimax polynomial
				// Range reduction: bring x to [-π, π]
				// Normalize to [-π, π]
				x = x - Double.Floor(x / Single.Tau) * Single.Tau;
				if (x > Single.Pi) x -= Single.Tau;
				if (x < -Single.Pi) x += Single.Tau;
				
				// Use symmetry: cos(-x) = cos(x)
				x = Double.Abs(x);
				
				// For x in [0, π], use polynomial approximation
				// Higher precision minimax polynomial for double
				var x2 = x * x;
				
				// Minimax polynomial coefficients optimized for double precision
				var ret = -1.1940250944959890e-7;  // x^10 term
				ret = Double.FusedMultiplyAdd(ret, x2, 2.0876755527587203e-5);   // x^8 term
				ret = Double.FusedMultiplyAdd(ret, x2, -0.0013888888888739916);  // x^6 term
				ret = Double.FusedMultiplyAdd(ret, x2, 0.041666666666666602);     // x^4 term
				ret = Double.FusedMultiplyAdd(ret, x2, -0.5);                     // x^2 term
				ret = Double.FusedMultiplyAdd(ret, x2, 1.0);                      // constant term
				
				return ret;
			}
			""";
	}
}


using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class CosFunctionOptimizer() : BaseFunctionOptimizer("Cos", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		// When FastMath is enabled, add a fast cos approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath
			&& paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastCosMethodFloat()
				: GenerateFastCosMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastCos", parameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, parameters);
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


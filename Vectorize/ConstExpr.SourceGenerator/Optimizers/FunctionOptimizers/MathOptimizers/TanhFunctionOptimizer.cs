using System;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class TanhFunctionOptimizer() : BaseMathFunctionOptimizer("Tanh", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(context.Method, out var paramType))
		{
			return false;
		}

		var x = context.VisitedParameters[0];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(x, out var value))
		{
			// Tanh(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = SyntaxHelpers.CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Tanh(Infinity) => 1
			if (double.IsPositiveInfinity(value))
			{
				result = SyntaxHelpers.CreateLiteral(1.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Tanh(-Infinity) => -1
			if (double.IsNegativeInfinity(value))
			{
				result = SyntaxHelpers.CreateLiteral((-1.0).ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastTanhMethodFloat()
				: GenerateFastTanhMethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastTanh", context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static bool TryGetNumericLiteral(ExpressionSyntax expr, out double value)
	{
		value = 0;
		switch (expr)
		{
			case LiteralExpressionSyntax { Token.Value: IConvertible c }:
				value = c.ToDouble(CultureInfo.InvariantCulture);
				return true;
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
				value = -c2.ToDouble(CultureInfo.InvariantCulture);
				return true;
			default:
				return false;
		}
	}

	private static string GenerateFastTanhMethodFloat()
	{
		return """
			private static float FastTanh(float x)
			{
				// Handle special cases
				if (Single.IsNaN(x)) return Single.NaN;
				if (x >= 5.0f) return 1.0f;  // Saturates to 1 for large positive values
				if (x <= -5.0f) return -1.0f; // Saturates to -1 for large negative values
				
				// For small values, use rational approximation
				// tanh(x) ? x * P(x�) / Q(x�) for |x| < 1
				// For larger values, use the identity: tanh(x) = (e^(2x) - 1) / (e^(2x) + 1)
				
				var absX = Single.Abs(x);
				
				if (absX < 1.0f)
				{
					// Rational approximation for small values
					var x2 = x * x;
					
					// Numerator coefficients for tanh(x) ? x * (1 + a1*x� + a2*x?) / (1 + b1*x� + b2*x?)
					var a1 = -0.3333314f;
					var a2 = 0.1333924f;
					var numerator = Single.FusedMultiplyAdd(a2, x2, a1);
					numerator = Single.FusedMultiplyAdd(numerator, x2, 1.0f);
					numerator *= x;
					
					var b1 = 1.0f;
					var b2 = -0.3333314f;
					var denominator = Single.FusedMultiplyAdd(b2, x2, b1);
					denominator = Single.FusedMultiplyAdd(denominator, x2, 1.0f);
					
					return numerator / denominator;
				}
				else
				{
					// Use exponential form for larger values
					var exp2x = Single.Exp(2.0f * x);
					return (exp2x - 1.0f) / (exp2x + 1.0f);
				}
			}
			""";
	}

	private static string GenerateFastTanhMethodDouble()
	{
		return """
			private static double FastTanh(double x)
			{
				// Handle special cases
				if (Double.IsNaN(x)) return Double.NaN;
				if (x >= 19.0) return 1.0;  // Saturates to 1 for large positive values
				if (x <= -19.0) return -1.0; // Saturates to -1 for large negative values
				
				// For small values, use high-precision rational approximation
				// For larger values, use exponential form
				
				var absX = Double.Abs(x);
				
				if (absX < 1.0)
				{
					// High-precision rational approximation for small values
					var x2 = x * x;
					
					// Numerator coefficients - minimax polynomial
					var a1 = -0.333333333333331;
					var a2 = 0.133333333333197;
					var a3 = -0.0539682539682505;
					var numerator = Double.FusedMultiplyAdd(a3, x2, a2);
					numerator = Double.FusedMultiplyAdd(numerator, x2, a1);
					numerator = Double.FusedMultiplyAdd(numerator, x2, 1.0);
					numerator *= x;
					
					var b1 = 1.0;
					var b2 = -0.133333333333197;
					var b3 = 0.0107936507936338;
					var denominator = Double.FusedMultiplyAdd(b3, x2, b2);
					denominator = Double.FusedMultiplyAdd(denominator, x2, b1);
					denominator = Double.FusedMultiplyAdd(denominator, x2, 1.0);
					
					return numerator / denominator;
				}
				else if (absX < 9.0)
				{
					// Use exponential form for medium values
					var exp2x = Double.Exp(2.0 * x);
					return (exp2x - 1.0) / (exp2x + 1.0);
				}
				else
				{
					// For very large values, use approximation: tanh(x) ? sign(x) * (1 - 2*e^(-2|x|))
					var exp2absX = Double.Exp(-2.0 * absX);
					return Double.CopySign(1.0 - 2.0 * exp2absX, x);
				}
			}
			""";
	}
}

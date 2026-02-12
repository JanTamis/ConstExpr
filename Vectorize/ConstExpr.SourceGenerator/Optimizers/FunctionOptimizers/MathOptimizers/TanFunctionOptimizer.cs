using System;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class TanFunctionOptimizer() : BaseMathFunctionOptimizer("Tan", 1)
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
			// Tan(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = SyntaxHelpers.CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Tan(π/4) => 1
			if (IsApproximately(value, Math.PI / 4.0))
			{
				result = SyntaxHelpers.CreateLiteral(1.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Tan(-π/4) => -1
			if (IsApproximately(value, -Math.PI / 4.0))
			{
				result = SyntaxHelpers.CreateLiteral((-1.0).ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Tan(π) => 0
			if (IsApproximately(value, Math.PI))
			{
				result = SyntaxHelpers.CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Tan(-π) => 0
			if (IsApproximately(value, -Math.PI))
			{
				result = SyntaxHelpers.CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastTanMethodFloat()
				: GenerateFastTanMethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastTan", context.VisitedParameters);
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

	private static string GenerateFastTanMethodFloat()
	{
		return """
			private static float FastTan(float x)
			{
				// Handle special cases
				if (Single.IsNaN(x)) return Single.NaN;
				if (Single.IsInfinity(x)) return Single.NaN;
				
				// Range reduction using Cody-Waite context.Method for better accuracy
				const float InvPi = 1.0f / Single.Pi
				
				// Reduce to [-π, π]
				var quotient = Single.Round(x * InvPi);
				var xReduced = Single.FusedMultiplyAdd(-quotient, Single.Pi, x);
				
				// Check if we're close to asymptotes at ±π/2
				var absX = Single.Abs(xReduced);
				if (absX > 1.4f) // Getting close to π/2 ≈ 1.5708
				{
					// Fall back to standard tan near asymptotes
					return Single.Tan(x);
				}
				
				// Minimax polynomial approximation for tan(x) in [-π/4, π/4]
				// Derived using Remez algorithm for optimal error distribution
				var x2 = xReduced * xReduced;
				
				// tan(x) ≈ x * P(x²) / Q(x²) where P and Q are polynomials
				// P(x²) = 1 + p1*x² + p2*x⁴
				var p1 = -0.1306282f;
				var p2 = 0.0052854f;
				var numerator = Single.FusedMultiplyAdd(p2, x2, p1);
				numerator = Single.FusedMultiplyAdd(numerator, x2, 1.0f);
				numerator *= xReduced;
				
				// Q(x²) = 1 + q1*x² + q2*x⁴
				var q1 = -0.4636476f;
				var q2 = 0.0157903f;
				var denominator = Single.FusedMultiplyAdd(q2, x2, q1);
				denominator = Single.FusedMultiplyAdd(denominator, x2, 1.0f);
				
				return numerator / denominator;
			}
			""";
	}

	private static string GenerateFastTanMethodDouble()
	{
		return """
			private static double FastTan(double x)
			{
				// Handle special cases
				if (Double.IsNaN(x)) return Double.NaN;
				if (Double.IsInfinity(x)) return Double.NaN;
				
				// Range reduction using Cody-Waite context.Method for better accuracy
				const double InvPi = 1.0 / Double.Pi;
				
				// Reduce to [-π, π]
				var quotient = Double.Round(x * InvPi);
				var xReduced = Double.FusedMultiplyAdd(-quotient, Double.Pi, x);
				
				// Check if we're close to asymptotes at ±π/2
				var absX = Double.Abs(xReduced);
				if (absX > 1.4) // Getting close to π/2 ≈ 1.5708
				{
					// Fall back to standard tan near asymptotes
					return Double.Tan(x);
				}
				
				// Minimax polynomial approximation for tan(x) in [-π/4, π/4]
				// Derived using Remez algorithm for optimal error distribution
				var x2 = xReduced * xReduced;
				
				// tan(x) ≈ x * P(x²) / Q(x²) where P and Q are polynomials
				// P(x²) = 1 + p1*x² + p2*x⁴ + p3*x⁶
				var p1 = -0.13089944486966634;
				var p2 = 0.005405742881796775;
				var p3 = -0.00010606776596208569;
				var numerator = Double.FusedMultiplyAdd(p3, x2, p2);
				numerator = Double.FusedMultiplyAdd(numerator, x2, p1);
				numerator = Double.FusedMultiplyAdd(numerator, x2, 1.0);
				numerator *= xReduced;
				
				// Q(x²) = 1 + q1*x² + q2*x⁴ + q3*x⁶
				var q1 = -0.46468849716162905;
				var q2 = 0.015893657956882884;
				var q3 = -0.00031920703894961204;
				var denominator = Double.FusedMultiplyAdd(q3, x2, q2);
				denominator = Double.FusedMultiplyAdd(denominator, x2, q1);
				denominator = Double.FusedMultiplyAdd(denominator, x2, 1.0);
				
				return numerator / denominator;
			}
			""";
	}
}

using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class TanPiFunctionOptimizer() : BaseFunctionOptimizer("TanPi", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		var x = parameters[0];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(x, out var value))
		{
			// TanPi(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = SyntaxHelpers.CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// TanPi(0.25) => 1 (tan(?/4) = 1)
			if (IsApproximately(value, 0.25))
			{
				result = SyntaxHelpers.CreateLiteral(1.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// TanPi(-0.25) => -1 (tan(-?/4) = -1)
			if (IsApproximately(value, -0.25))
			{
				result = SyntaxHelpers.CreateLiteral((-1.0).ToSpecialType(paramType.SpecialType));
				return true;
			}

			// TanPi(0.5) => undefined (asymptote at ?/2), but mathematically approaches infinity
			// TanPi(1.0) => 0 (tan(?) = 0)
			if (IsApproximately(value, 1.0))
			{
				result = SyntaxHelpers.CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// TanPi(-1.0) => 0 (tan(-?) = 0)
			if (IsApproximately(value, -1.0))
			{
				result = SyntaxHelpers.CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		// When FastMath is enabled, add a fast tanpi approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath
			&& paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastTanPiMethodFloat()
				: GenerateFastTanPiMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastTanPi", parameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, parameters);
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

	private static string GenerateFastTanPiMethodFloat()
	{
		return """
			private static float FastTanPi(float x)
			{
				// Handle special cases
				if (Single.IsNaN(x)) return Single.NaN;
				if (Single.IsInfinity(x)) return Single.NaN;
				
				// Range reduction to [-0.5, 0.5]
				// TanPi(x) has period 1, so reduce x modulo 1
				var xReduced = x - Single.Round(x);
				
				// Check if we're close to asymptotes at �0.5 (�?/2)
				var absX = Single.Abs(xReduced);
				if (absX > 0.45f) // Getting close to 0.5
				{
					// Fall back to standard TanPi near asymptotes
					return Single.TanPi(x);
				}
				
				// Convert to radians: x * ?
				var xRadians = xReduced * Single.Pi;
				
				// Minimax polynomial approximation for tan(?*x) in [-0.45, 0.45]
				// Using the same polynomial coefficients as regular tan
				var x2 = xRadians * xRadians;
				
				// tan(x) ? x * P(x�) / Q(x�) where P and Q are polynomials
				// P(x�) = 1 + p1*x� + p2*x?
				var p1 = -0.1306282f;
				var p2 = 0.0052854f;
				var numerator = Single.FusedMultiplyAdd(p2, x2, p1);
				numerator = Single.FusedMultiplyAdd(numerator, x2, 1.0f);
				numerator *= xRadians;
				
				// Q(x�) = 1 + q1*x� + q2*x?
				var q1 = -0.4636476f;
				var q2 = 0.0157903f;
				var denominator = Single.FusedMultiplyAdd(q2, x2, q1);
				denominator = Single.FusedMultiplyAdd(denominator, x2, 1.0f);
				
				return numerator / denominator;
			}
			""";
	}

	private static string GenerateFastTanPiMethodDouble()
	{
		return """
			private static double FastTanPi(double x)
			{
				// Handle special cases
				if (Double.IsNaN(x)) return Double.NaN;
				if (Double.IsInfinity(x)) return Double.NaN;
				
				// Range reduction to [-0.5, 0.5]
				// TanPi(x) has period 1, so reduce x modulo 1
				var xReduced = x - Double.Round(x);
				
				// Check if we're close to asymptotes at �0.5 (�?/2)
				var absX = Double.Abs(xReduced);
				if (absX > 0.45) // Getting close to 0.5
				{
					// Fall back to standard TanPi near asymptotes
					return Double.TanPi(x);
				}
				
				// Convert to radians: x * ?
				var xRadians = xReduced * Double.Pi;
				
				// Minimax polynomial approximation for tan(?*x) in [-0.45, 0.45]
				// Using the same polynomial coefficients as regular tan
				var x2 = xRadians * xRadians;
				
				// tan(x) ? x * P(x�) / Q(x�) where P and Q are polynomials
				// P(x�) = 1 + p1*x� + p2*x? + p3*x?
				var p1 = -0.13089944486966634;
				var p2 = 0.005405742881796775;
				var p3 = -0.00010606776596208569;
				var numerator = Double.FusedMultiplyAdd(p3, x2, p2);
				numerator = Double.FusedMultiplyAdd(numerator, x2, p1);
				numerator = Double.FusedMultiplyAdd(numerator, x2, 1.0);
				numerator *= xRadians;
				
				// Q(x�) = 1 + q1*x� + q2*x? + q3*x?
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

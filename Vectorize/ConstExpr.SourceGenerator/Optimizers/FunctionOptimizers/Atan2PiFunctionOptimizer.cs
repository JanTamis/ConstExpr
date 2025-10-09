using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class Atan2PiFunctionOptimizer() : BaseFunctionOptimizer("Atan2Pi", 2)
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		var y = parameters[0];
		var x = parameters[1];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(y, out var yValue) && TryGetNumericLiteral(x, out var xValue))
		{
			// Atan2Pi(0, x) where x > 0 => 0
			if (IsApproximately(yValue, 0.0) && xValue > 0.0)
			{
				result = SyntaxHelpers.CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2Pi(0, x) where x < 0 => 1 (π/π = 1)
			if (IsApproximately(yValue, 0.0) && xValue < 0.0)
			{
				result = SyntaxHelpers.CreateLiteral(1.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2Pi(y, 0) where y > 0 => 0.5 (π/2 / π = 0.5)
			if (IsApproximately(xValue, 0.0) && yValue > 0.0)
			{
				result = SyntaxHelpers.CreateLiteral(0.5.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2Pi(y, 0) where y < 0 => -0.5
			if (IsApproximately(xValue, 0.0) && yValue < 0.0)
			{
				result = SyntaxHelpers.CreateLiteral((-0.5).ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2Pi(y, x) where y == x and x > 0 => 0.25 (π/4 / π = 0.25)
			if (IsApproximately(yValue, xValue) && xValue > 0.0)
			{
				result = SyntaxHelpers.CreateLiteral(0.25.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		// When FastMath is enabled, add a fast atan2pi approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath
			&& paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastAtan2PiMethodFloat()
				: GenerateFastAtan2PiMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastAtan2Pi", parameters);
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
				value = c.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
				return true;
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
				value = -c2.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
				return true;
			default:
				return false;
		}
	}

	private static string GenerateFastAtan2PiMethodFloat()
	{
		return """
			private static float FastAtan2Pi(float y, float x)
			{
				// Handle special cases
				if (Single.IsNaN(y) || Single.IsNaN(x)) return Single.NaN;
				if (y == 0.0f && x == 0.0f) return 0.0f;
				
				var absY = Single.Abs(y);
				var absX = Single.Abs(x);

				// Determine quadrant and calculate base angle
				var angle = 0.0f;
				
				if (absX >= absY)
				{
					// Use atan(y/x) approximation
					var z = y / x;
					var z2 = z * z;
					var z4 = z2 * z2;
					
					// numerator = z * (15 + 4*z^2) using FMA
					var numerator = Single.FusedMultiplyAdd(4.0f, z2, 15.0f);
					numerator *= z;
					
					// denominator = 15 + 9*z^2 + z^4 using FMA
					var denominator = Single.FusedMultiplyAdd(9.0f, z2, 15.0f);
					denominator = z4 + denominator;
					
					angle = numerator / denominator;
					
					// Adjust for negative x (quadrants II and III)
					if (Single.IsNegative(x))
					{
						angle = y >= 0.0f ? Single.Pi + angle : angle - Single.Pi;
					}
				}
				else
				{
					// Use atan(x/y) and adjust
					var z = x / y;
					var z2 = z * z;
					var z4 = z2 * z2;
					
					// numerator = z * (15 + 4*z^2) using FMA
					var numerator = Single.FusedMultiplyAdd(4.0f, z2, 15.0f);
					numerator *= z;
					
					// denominator = 15 + 9*z^2 + z^4 using FMA
					var denominator = Single.FusedMultiplyAdd(9.0f, z2, 15.0f);
					denominator = z4 + denominator;
					
					var baseAngle = numerator / denominator;

					angle = y >= 0.0f ? Single.Pi / 2 - baseAngle : -Single.Pi / 2 - baseAngle; // ±π/2
				}
				
				return angle * (1d / Single.Pi); // 1/π
			}
			""";
	}

	private static string GenerateFastAtan2PiMethodDouble()
	{
		return """
			private static double FastAtan2Pi(double y, double x)
			{
				// Handle special cases
				if (Double.IsNaN(y) || Double.IsNaN(x)) return Double.NaN;
				if (y == 0.0 && x == 0.0) return 0.0;
				
				var absY = Math.Abs(y);
				var absX = Math.Abs(x);
				
				// Determine quadrant and calculate base angle
				var angle = 0.0;
				
				if (absX >= absY)
				{
					// Use atan(y/x) approximation
					var z = y / x;
					var z2 = z * z;
					var z4 = z2 * z2;
					
					// numerator = z * (15 + 4*z^2) using FMA
					var numerator = Double.FusedMultiplyAdd(4.0, z2, 15.0);
					numerator *= z;
					
					// denominator = 15 + 9*z^2 + z^4 using FMA
					var denominator = Double.FusedMultiplyAdd(9.0, z2, 15.0);
					denominator = z4 + denominator;
					
					angle = numerator / denominator;
					
					// Adjust for negative x (quadrants II and III)
					if (Double.IsNegative(x))
					{
						angle = y >= 0.0 ? Double.Pi + angle : angle - Double.Pi;
					}
				}
				else
				{
					// Use atan(x/y) and adjust
					var z = x / y;
					var z2 = z * z;
					var z4 = z2 * z2;
					
					// numerator = z * (15 + 4*z^2) using FMA
					var numerator = Double.FusedMultiplyAdd(4.0, z2, 15.0);
					numerator *= z;
					
					// denominator = 15 + 9*z^2 + z^4 using FMA
					var denominator = Double.FusedMultiplyAdd(9.0, z2, 15.0);
					denominator = z4 + denominator;
					
					var baseAngle = numerator / denominator;

					angle = y >= 0.0 ? Double.Pi / 2 - baseAngle : -Double.Pi / 2 - baseAngle; // ±π/2
				}
				
				return angle * (1d / Double.Pi); // 1/π
			}
			""";
	}
}

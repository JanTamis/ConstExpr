using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class Atan2FunctionOptimizer() : BaseFunctionOptimizer("Atan2", 2)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
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
			// Atan2(0, x) where x > 0 => 0
			if (IsApproximately(yValue, 0.0) && xValue > 0.0)
			{
				result = SyntaxHelpers.CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2(0, x) where x < 0 => π
			if (IsApproximately(yValue, 0.0) && xValue < 0.0)
			{
				result = SyntaxHelpers.CreateLiteral(Math.PI.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2(y, 0) where y > 0 => π/2
			if (IsApproximately(xValue, 0.0) && yValue > 0.0)
			{
				var piOver2 = Math.PI / 2.0;
				result = SyntaxHelpers.CreateLiteral(piOver2.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2(y, 0) where y < 0 => -π/2
			if (IsApproximately(xValue, 0.0) && yValue < 0.0)
			{
				var negPiOver2 = -Math.PI / 2.0;
				result = SyntaxHelpers.CreateLiteral(negPiOver2.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2(y, x) where y == x => π/4 or -3π/4
			if (IsApproximately(yValue, xValue) && xValue > 0.0)
			{
				var piOver4 = Math.PI / 4.0;
				result = SyntaxHelpers.CreateLiteral(piOver4.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		// When FastMath is enabled, add a fast atan2 approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath
			&& paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastAtan2MethodFloat()
				: GenerateFastAtan2MethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastAtan2", parameters);
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

	private static string GenerateFastAtan2MethodFloat()
	{
		return """
			private static float FastAtan2(float y, float x)
			{
				// Handle special cases
				if (float.IsNaN(y) || Single.IsNaN(x)) return Single.NaN;
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
						angle = y >= 0.0f ? Single.Pi + angle : angle - Single.Pi; // π
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
				
				return angle;
			}
			""";
	}

	private static string GenerateFastAtan2MethodDouble()
	{
		return """
			private static double FastAtan2(double y, double x)
			{
				// Handle special cases
				if (Double.IsNaN(y) || Double.IsNaN(x)) return Double.NaN;
				if (y == 0.0 && x == 0.0) return 0.0;
				
				var absY = Double.Abs(y);
				var absX = Double.Abs(x);
				
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
					
					angle = y >= 0.0f ? Double.Pi / 2 - baseAngle : -Double.Pi / 2 - baseAngle; // ±π/2
				}
				
				return angle;
			}
			""";
	}
}

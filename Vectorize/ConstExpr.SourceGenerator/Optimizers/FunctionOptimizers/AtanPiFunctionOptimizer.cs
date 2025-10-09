using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class AtanPiFunctionOptimizer() : BaseFunctionOptimizer("AtanPi", 1)
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
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
			// AtanPi(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = SyntaxHelpers.CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// AtanPi(1) => 0.25 (π/4 / π = 0.25)
			if (IsApproximately(value, 1.0))
			{
				result = SyntaxHelpers.CreateLiteral(0.25.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// AtanPi(-1) => -0.25
			if (IsApproximately(value, -1.0))
			{
				result = SyntaxHelpers.CreateLiteral((-0.25).ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		// When FastMath is enabled, add a fast atanpi approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath
			&& paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastAtanPiMethodFloat()
				: GenerateFastAtanPiMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastAtanPi", parameters);
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

	private static string GenerateFastAtanPiMethodFloat()
	{
		return """
			private static float FastAtanPi(float x)
			{
				// Handle special cases
				if (Single.IsNaN(x)) return Single.NaN;
				if (Single.IsPositiveInfinity(x)) return 0.5f; // π/2 / π = 0.5
				if (Single.IsNegativeInfinity(x)) return -0.5f;

				// Range reduction for better accuracy
				var absX = Single.Abs(x);
				
				var useReciprocal = absX > 1.0f;
				var z = useReciprocal ? Single.Reciprocal(absX) : absX;

				// Padé approximant (inlined constants)
				var z2 = z * z;
				var z4 = z2 * z2;
				
				// numerator = z * (15 + 4*z^2) using FMA
				var numerator = Single.FusedMultiplyAdd(4.0f, z2, 15.0f);
				numerator *= z;
				
				// denominator = 15 + 9*z^2 + z^4 using FMA
				var denominator = Single.FusedMultiplyAdd(9.0f, z2, 15.0f);
				denominator = z4 + denominator;
				
				var result = numerator / denominator;
				
				// Adjust for reciprocal transformation
				if (useReciprocal)
				{
					result = Single.Pi / 2f - result; // π/2 - result
				}
				
				return Single.CopySign(result * (1f / Single.Pi), x);
			}
			""";
	}

	private static string GenerateFastAtanPiMethodDouble()
	{
		return """
			private static double FastAtanPi(double x)
			{
				// Handle special cases
				if (Double.IsNaN(x)) return Double.NaN;
				if (Double.IsPositiveInfinity(x)) return 0.5; // π/2 / π = 0.5
				if (Double.IsNegativeInfinity(x)) return -0.5;

				// Range reduction for better accuracy
				var absX = Double.Abs(x);
				
				var useReciprocal = absX > 1.0;
				var z = useReciprocal ? Double.Reciprocal(absX) : absX;

				// Padé approximant (inlined constants)
				var z2 = z * z;
				var z4 = z2 * z2;
				
				// numerator = z * (15 + 4*z^2) using FMA
				var numerator = Double.FusedMultiplyAdd(4.0, z2, 15.0);
				numerator *= z;
				
				// denominator = 15 + 9*z^2 + z^4 using FMA
				var denominator = Double.FusedMultiplyAdd(9.0, z2, 15.0);
				denominator = z4 + denominator;
				
				var result = numerator / denominator;
				
				// Adjust for reciprocal transformation
				if (useReciprocal)
				{
					result = Double.Pi / 2 - result; // π/2 - result
				}

				return Single.CopySign(result * (1f / Single.Pi), x);
			}
			""";
	}
}

using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class AtanFunctionOptimizer() : BaseFunctionOptimizer("Atan", 1)
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
			// Atan(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = SyntaxHelpers.CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan(1) => π/4
			if (IsApproximately(value, 1.0))
			{
				var piOver4 = Math.PI / 4.0;
				result = SyntaxHelpers.CreateLiteral(piOver4.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan(-1) => -π/4
			if (IsApproximately(value, -1.0))
			{
				var negPiOver4 = -Math.PI / 4.0;
				result = SyntaxHelpers.CreateLiteral(negPiOver4.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		// When FastMath is enabled, add a fast atan approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath
			&& paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastAtanMethodFloat()
				: GenerateFastAtanMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastAtan", parameters);
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

	private static string GenerateFastAtanMethodFloat()
	{
		return """
			private static float FastAtan(float x)
			{
				// Handle special cases
				if (Single.IsNaN(x)) return Single.NaN;
				if (Single.IsPositiveInfinity(x)) return Single.Pi / 2; // π/2
				if (Single.IsNegativeInfinity(x)) return -Single.Pi / 2; // -π/2

				var absX = Single.Abs(x);
				var sign = Single.Sign(x);
				
				var useReciprocal = absX > 1.0f;
				var z = useReciprocal ? Single.ReciprocalEstimate(absX) : absX;
				
				// Padé approximant coefficients (inlined)
				var z2 = z * z;
				var z4 = z2 * z2;
				
				// numerator = z * (15 + 4*z^2)
				var numerator = Single.FusedMultiplyAdd(4.0f, z2, 15.0f);
				numerator *= z;
				
				// denominator = 15 + 9*z^2 + z^4
				var denominator = Single.FusedMultiplyAdd(9.0f, z2, 15.0f);
				denominator = z4 + denominator;

				var result = numerator / denominator;
				
				// Adjust for reciprocal transformation
				if (useReciprocal)
				{
					result = Single.Pi / 2 - result; // π/2 - result
				}
				
				return sign * result;
			}
			""";
	}

	private static string GenerateFastAtanMethodDouble()
	{
		return """
			private static double FastAtan(double x)
			{
				// Handle special cases
				if (Double.IsNaN(x)) return Double.NaN;
				if (Double.IsPositiveInfinity(x)) return Double.Pi / 2; // π/2
				if (Double.IsNegativeInfinity(x)) return -Double.Pi / 2; // -π/2

				var absX = Double.Abs(x);
				var sign = Double.Sign(x);
				
				var useReciprocal = absX > 1.0;
				var z = useReciprocal ? Double.ReciprocalEstimate(absX) : absX;

				// Padé approximant coefficients (inlined)
				var z2 = z * z;
				var z4 = z2 * z2;
				
				// numerator = z * (15 + 4*z^2)
				var numerator = Double.FusedMultiplyAdd(4.0, z2, 15.0);
				numerator *= z;
				
				// denominator = 15 + 9*z^2 + z^4
				var denominator = Double.FusedMultiplyAdd(9.0, z2, 15.0);
				denominator = z4 + denominator;

				var result = numerator / denominator;
				
				// Adjust for reciprocal transformation
				if (useReciprocal)
				{
					result = Double.Pi / 2 - result; // π/2 - result
				}
				
				return sign * result;
			}
			""";
	}
}

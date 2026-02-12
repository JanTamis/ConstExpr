using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Helpers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AtanhFunctionOptimizer() : BaseMathFunctionOptimizer("Atanh", 1)
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
			// Atanh(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = SyntaxHelpers.CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atanh(1) => ∞, Atanh(-1) => -∞ (domain boundary)
			if (IsApproximately(Math.Abs(value), 1.0))
			{
				var inf = value > 0
					? paramType.SpecialType == SpecialType.System_Single ? float.PositiveInfinity : double.PositiveInfinity
					: paramType.SpecialType == SpecialType.System_Single ? float.NegativeInfinity : double.NegativeInfinity;
				result = SyntaxHelpers.CreateLiteral(inf.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastAtanhMethodFloat()
				: GenerateFastAtanhMethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastAtanh", context.VisitedParameters);
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
				value = c.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
				return true;
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
				value = -c2.ToDouble(System.Globalization.CultureInfo.InvariantCulture);
				return true;
			default:
				return false;
		}
	}

	private static string GenerateFastAtanhMethodFloat()
	{
		return """
			private static float FastAtanh(float x)
			{
				// Handle special cases
				if (Single.IsNaN(x)) return Single.NaN;
				if (Single.Abs(x) >= 1.0f) return x > 0 ? float.PositiveInfinity : float.NegativeInfinity;

				// Use the definition: atanh(x) = 0.5 * ln((1 + x) / (1 - x))
				// For small |x|, use Taylor series for better accuracy
				var absX = Single.Abs(x);
				
				if (absX < 0.5f)
				{
					// Taylor series: atanh(x) = x + x³/3 + x⁵/5 + x⁷/7 + x⁹/9
					var x2 = x * x;
					
					// Horner's context.Method with FMA: x * (1 + x²*(1/3 + x²*(1/5 + x²*(1/7 + x²/9))))
					var poly = Single.FusedMultiplyAdd(x2, 1f / 9f, 1f / 7f); // 1/9, 1/7
					poly = Single.FusedMultiplyAdd(poly, x2, 0.2f); // 1/5
					poly = Single.FusedMultiplyAdd(poly, x2, 1f / 3f); // 1/3
					poly = Single.FusedMultiplyAdd(poly, x2, 1f);

					return x * poly;
				}
				else
				{
					// Use logarithmic formula: 0.5 * ln((1 + x) / (1 - x))
					return 0.5f * Single.Log((1f + x) / (1f - x));
				}
			}
			""";
	}

	private static string GenerateFastAtanhMethodDouble()
	{
		return """
			private static double FastAtanh(double x)
			{
				// Handle special cases
				if (Double.IsNaN(x)) return Double.NaN;
				if (Math.Abs(x) >= 1.0) return x > 0 ? Double.PositiveInfinity : Double.NegativeInfinity;

				// Use the definition: atanh(x) = 0.5 * ln((1 + x) / (1 - x))
				// For small |x|, use Taylor series for better accuracy
				var absX = Double.Abs(x);
				
				if (absX < 0.5)
				{
					// Taylor series: atanh(x) = x + x³/3 + x⁵/5 + x⁷/7 + x⁹/9 + x¹¹/11
					var x2 = x * x;
					
					// Horner's context.Method with FMA: x * (1 + x²*(1/3 + x²*(1/5 + x²*(1/7 + x²*(1/9 + x²/11)))))
					var poly = Double.FusedMultiplyAdd(x2, 1d / 11d, 1d / 9d); // 1/11, 1/9
					poly = Double.FusedMultiplyAdd(poly, x2, 1d / 7d); // 1/7
					poly = Double.FusedMultiplyAdd(poly, x2, 1d / 5d); // 1/5
					poly = Double.FusedMultiplyAdd(poly, x2, 1d / 3d); // 1/3
					poly = Double.FusedMultiplyAdd(poly, x2, 1d);

					return x * poly;
				}
				else
				{
					// Use logarithmic formula: 0.5 * ln((1 + x) / (1 - x))
					return 0.5 * Double.Log((1.0 + x) / (1.0 - x));
				}
			}
			""";
	}
}

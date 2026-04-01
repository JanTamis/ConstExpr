using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AtanPiFunctionOptimizer() : BaseMathFunctionOptimizer("AtanPi", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(arg, out var value))
		{
			// AtanPi(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// AtanPi(1) => 0.25 (π/4 / π = 0.25)
			if (IsApproximately(value, 1.0))
			{
				result = CreateLiteral(0.25.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// AtanPi(-1) => -0.25
			if (IsApproximately(value, -1.0))
			{
				result = CreateLiteral((-0.25).ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastAtanPiMethodFloat()
				: GenerateFastAtanPiMethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastAtanPi", context.VisitedParameters);
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

	private static string GenerateFastAtanPiMethodFloat()
	{
		return """
			private static float FastAtanPi(float x)
			{
				// NaN propagates; +/-Inf → +/-0.5 via swap path (1/Inf = 0 → p=0 → 0.5)
				if (Single.IsNaN(x)) return Single.NaN;
				var absX = Single.Abs(x);
				var swap = absX > 1.0f;
				var a = swap ? 1.0f / absX : absX;

				// Steinmetz 2-term: atanpi(a) ≈ a*(0.25 + (0.273/π)*(1−a))
				// = FMA(−0.273/π, a, 0.25+0.273/π) * a  — 1 FMA + 1 mul
				// 0.273/π ≈ 0.086916; 0.25 + 0.086916 = 0.336916
				// atanpi(0)=0 and atanpi(1)=0.25 hold exactly.
				// Max absolute error ≈ 1.6e-3.
				var p = Single.FusedMultiplyAdd(-0.086916f, a, 0.336916f) * a;

				p = swap ? 0.5f - p : p;
				return Single.IsNegative(x) ? -p : p;
			}
			""";
	}

	private static string GenerateFastAtanPiMethodDouble()
	{
		return """
			private static double FastAtanPi(double x)
			{
				// NaN propagates; +/-Inf → +/-0.5 via swap path (1/Inf = 0 → p=0 → 0.5)
				if (Double.IsNaN(x)) return Double.NaN;
				var absX = Double.Abs(x);
				var swap = absX > 1.0;
				var a = swap ? 1.0 / absX : absX;
				var u = a * a;

				// A&S §4.4.43 minimax coefficients pre-divided by π — saves the final 1/π multiply.
				// Quadrant correction uses 0.5 (= π/2 / π). Max absolute error ≈ 3.5e-6.
				var p = Double.FusedMultiplyAdd(u,  0.00663222, -0.02710107);
				p      = Double.FusedMultiplyAdd(u, p,           0.05733014);
				p      = Double.FusedMultiplyAdd(u, p,          -0.10510700);
				p      = Double.FusedMultiplyAdd(u, p,           0.31826720);
				p     *= a;

				p = swap ? 0.5 - p : p;
				return Double.IsNegative(x) ? -p : p;
			}
			""";
	}
}

using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Atan2FunctionOptimizer() : BaseMathFunctionOptimizer("Atan2", 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var y = context.VisitedParameters[0];
		var x = context.VisitedParameters[1];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(y, out var yValue) && TryGetNumericLiteral(x, out var xValue))
		{
			// Atan2(0, x) where x > 0 => 0
			if (IsApproximately(yValue, 0.0) && xValue > 0.0)
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2(0, x) where x < 0 => π
			if (IsApproximately(yValue, 0.0) && xValue < 0.0)
			{
				result = CreateLiteral(Math.PI.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2(y, 0) where y > 0 => π/2
			if (IsApproximately(xValue, 0.0) && yValue > 0.0)
			{
				var piOver2 = Math.PI / 2.0;
				result = CreateLiteral(piOver2.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2(y, 0) where y < 0 => -π/2
			if (IsApproximately(xValue, 0.0) && yValue < 0.0)
			{
				var negPiOver2 = -Math.PI / 2.0;
				result = CreateLiteral(negPiOver2.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan2(y, x) where y == x => π/4 or -3π/4
			if (IsApproximately(yValue, xValue) && xValue > 0.0)
			{
				var piOver4 = Math.PI / 4.0;
				result = CreateLiteral(piOver4.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastAtan2MethodFloat()
				: GenerateFastAtan2MethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastAtan2", context.VisitedParameters);
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

	private static string GenerateFastAtan2MethodFloat()
	{
		return """
			private static float FastAtan2(float y, float x)
			{
				// Special cases
				if (Single.IsNaN(y) || Single.IsNaN(x)) return Single.NaN;
				var absX = Single.Abs(x);
				var absY = Single.Abs(y);
				var maxV = Single.Max(absX, absY);
				if (maxV == 0f) return 0f;
				
				// Octant reduction: a = min(|x|,|y|) / max(|x|,|y|) ∈ [0, 1]
				var a = Single.Min(absX, absY) / maxV;
				var u = a * a;
				
				// Abramowitz & Stegun §4.4.43 minimax polynomial for atan(a)/a on [0, 1].
				// 5 coefficients, max absolute error ≈ 1.1e-5 rad.
				var p = Single.FusedMultiplyAdd(u,  0.0208351f, -0.0851330f);
				p = Single.FusedMultiplyAdd(u, p,  0.1801410f);
				p = Single.FusedMultiplyAdd(u, p, -0.3302995f);
				p = Single.FusedMultiplyAdd(u, p,  0.9998660f);
				p *= a;
				
				// Octant and quadrant corrections — conditional-move friendly
				p = absY > absX ? Single.Pi / 2 - p : p;
				p = x < 0f     ? Single.Pi      - p : p;
				p = y < 0f     ? -p : p;
				return p;
			}
			""";
	}

	private static string GenerateFastAtan2MethodDouble()
	{
		return """
			private static double FastAtan2(double y, double x)
			{
				// Special cases
				if (Double.IsNaN(y) || Double.IsNaN(x)) return Double.NaN;
				var absX = Double.Abs(x);
				var absY = Double.Abs(y);
				var maxV = Double.Max(absX, absY);
				if (maxV == 0.0) return 0.0;
				
				// Octant reduction: a = min(|x|,|y|) / max(|x|,|y|) ∈ [0, 1]
				var a = Double.Min(absX, absY) / maxV;
				
				// Half-angle identity: atan(a) = 2·atan(t),  t = a / (1 + sqrt(1 + a²))
				// Maps a ∈ [0, 1] → t ∈ [0, tan(π/8)] ≈ [0, 0.4142]; ensures fast Taylor convergence.
				var t = a / (1.0 + Double.Sqrt(1.0 + a * a));
				var u = t * t;
				
				// 8-term Horner Taylor series: atan(t)/t = Σ (−u)ⁿ/(2n+1).
				// Truncation error ≤ t¹⁷/17 ≈ 1.8e-8 rad; total error after 2× ≈ 4e-8 rad.
				var p = Double.FusedMultiplyAdd(u, -1.0 / 15.0,  1.0 / 13.0);
				p = Double.FusedMultiplyAdd(u, p, -1.0 / 11.0);
				p = Double.FusedMultiplyAdd(u, p,  1.0 / 9.0);
				p = Double.FusedMultiplyAdd(u, p, -1.0 / 7.0);
				p = Double.FusedMultiplyAdd(u, p,  1.0 / 5.0);
				p = Double.FusedMultiplyAdd(u, p, -1.0 / 3.0);
				p = Double.FusedMultiplyAdd(u, p,  1.0);
				p = 2.0 * t * p; // atan(a) = 2·atan(t)
				
				// Octant and quadrant corrections — conditional-move friendly
				p = absY > absX ? Double.Pi / 2 - p : p;
				p = x < 0.0    ? Double.Pi      - p : p;
				p = y < 0.0    ? -p : p;
				return p;
			}
			""";
	}
}

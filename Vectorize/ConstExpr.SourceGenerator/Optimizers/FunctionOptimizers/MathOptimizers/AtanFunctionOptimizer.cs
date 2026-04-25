using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AtanFunctionOptimizer() : BaseMathFunctionOptimizer("Atan", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(arg, out var value))
		{
			// Atan(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan(1) => π/4
			if (IsApproximately(value, 1.0))
			{
				var piOver4 = Math.PI / 4.0;
				result = CreateLiteral(piOver4.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atan(-1) => -π/4
			if (IsApproximately(value, -1.0))
			{
				var negPiOver4 = -Math.PI / 4.0;
				result = CreateLiteral(negPiOver4.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAtanMethodFloat(),
			SpecialType.System_Double => GenerateFastAtanMethodDouble(),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
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
			{
				value = c.ToDouble(CultureInfo.InvariantCulture);
				return true;
			}
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken, Operand: LiteralExpressionSyntax { Token.Value: IConvertible c2 } }:
			{
				value = -c2.ToDouble(CultureInfo.InvariantCulture);
				return true;
			}
			default:
			{
				return false;
			}
		}
	}

	private static string GenerateFastAtanMethodFloat()
	{
		return """
			private static float FastAtan(float x)
			{
				// NaN propagates naturally through the polynomial; ±Inf is handled correctly
				// because 1/±Inf = 0, giving atan(±Inf) = ±π/2 without an explicit branch.
				if (Single.IsNaN(x)) return Single.NaN;
				var absX = Single.Abs(x);
				var swap = absX > 1.0f;
				var a = swap ? 1.0f / absX : absX; // exact reciprocal — no ReciprocalEstimate loss

				// A&S §4.4.43 minimax polynomial: atan(a)/a ≈ c₁ + u*(c₃ + u*(c₅ + u*(c₇ + u*c₉)))
				// 4 FMAs + 1 mul; max absolute error ≈ 1.1e-5 rad (~2000× better than Padé [2/2]).
				var u = a * a;
				var p = Single.FusedMultiplyAdd(u,  0.0208351f, -0.0851330f);
				p      = Single.FusedMultiplyAdd(u, p,           0.1801410f);
				p      = Single.FusedMultiplyAdd(u, p,          -0.3302995f);
				p      = Single.FusedMultiplyAdd(u, p,           0.9998660f);
				p     *= a;

				// atan(x) = π/2 − atan(1/|x|) when |x| > 1; restore original sign
				p = swap ? Single.Pi / 2 - p : p;
				return Single.IsNegative(x) ? -p : p;
			}
			""";
	}

	private static string GenerateFastAtanMethodDouble()
	{
		return """
			private static double FastAtan(double x)
			{
				// NaN propagates naturally through the polynomial; ±Inf is handled correctly
				// because 1/±Inf = 0, giving atan(±Inf) = ±π/2 without an explicit branch.
				if (Double.IsNaN(x)) return Double.NaN;
				var absX = Double.Abs(x);
				var swap = absX > 1.0;
				var a = swap ? 1.0 / absX : absX; // exact reciprocal — no ReciprocalEstimate loss

				// A&S §4.4.43 minimax polynomial: atan(a)/a ≈ c₁ + u*(c₃ + u*(c₅ + u*(c₇ + u*c₉)))
				// 4 FMAs + 1 mul; max absolute error ≈ 1.1e-5 rad (~2000× better than Padé [2/2]).
				var u = a * a;
				var p = Double.FusedMultiplyAdd(u,  0.0208351, -0.0851330);
				p      = Double.FusedMultiplyAdd(u, p,          0.1801410);
				p      = Double.FusedMultiplyAdd(u, p,         -0.3302995);
				p      = Double.FusedMultiplyAdd(u, p,          0.9998660);
				p     *= a;

				// atan(x) = π/2 − atan(1/|x|) when |x| > 1; restore original sign
				p = swap ? Double.Pi / 2 - p : p;
				return Double.IsNegative(x) ? -p : p;
			}
			""";
	}
}
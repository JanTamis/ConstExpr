using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class PowFunctionOptimizer() : BaseMathFunctionOptimizer("Pow", n => n is 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var x = context.VisitedParameters[0];
		var y = context.VisitedParameters[1];

		// Algebraic simplifications on literal exponents (safe and type-preserving)
		if (TryGetNumericLiteral(y, out var exp))
		{
			// x^0 => 1
			if (IsApproximately(exp, 0.0))
			{
				result = LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1.0));
				return true;
			}

			// x^(-1) => Reciprocal(x) bij fast-math, anders 1/x
			if (IsApproximately(exp, -1.0) && IsPure(x))
			{
				if (HasMethod(paramType, "Reciprocal", 1))
				{
					result = CreateInvocation(paramType, "Reciprocal", x);
					return true;
				}

				var div = DivideExpression(
					CreateLiteral(1.0.ToSpecialType(paramType.SpecialType)), x);

				result = ParenthesizedExpression(div);
				return true;
			}

			// x^n => x * x * ... * x for small integer n
			if (Math.Abs(exp) > 1.0 && Math.Abs(exp) <= 5.0 && IsPure(x) && Math.Abs(exp - Math.Round(exp)) < Double.Epsilon)
			{
				var n = (int) Math.Round(exp);
				var acc = x;

				for (var i = 1; i < Math.Abs(n); i++)
				{
					acc = MultiplyExpression(acc, x);
				}

				if (n < 0)
				{
					acc = DivideExpression(
						LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1.0)), acc);
				}

				result = ParenthesizedExpression(acc);
				return true;
			}

			// x^1 => x
			if (IsApproximately(exp, 1.0))
			{
				result = x;
				return true;
			}

			// x^2 => (x * x) when x is pure (no side-effects)
			if (IsApproximately(exp, 2.0) && IsPure(x))
			{
				var mul = MultiplyExpression(x, x);
				result = ParenthesizedExpression(mul);
				return true;
			}

			// x^(1 / 2) => Sqrt(x)
			if (IsApproximately(exp, 1 / 2.0) && HasMethod(paramType, "Sqrt", 1))
			{
				result = CreateInvocation(paramType, "Sqrt", x);
				return true;
			}

			// x^(1 / 3) => Cbrt(x)
			if (IsApproximately(exp, 1 / 3.0) && HasMethod(paramType, "Cbrt", 1))
			{
				result = CreateInvocation(paramType, "Cbrt", x);
				return true;
			}

			// x^(1 / n) => RootN(x, n) for small integer n
			if (IsApproximately(1 / exp, Math.Floor(1 / exp)))
			{
				result = CreateInvocation(paramType, "RootN", x, CreateLiteral((int) Math.Round(1 / exp)));
				return true;
			}
		}

		// Benchmark results (Apple M4 Pro, .NET 10.0.1, ARM64 RyuJIT):
		//   Double: FastPow 2.965 ns vs Math.Pow 4.943 ns → 1.67× faster   ← inject FastPow
		//   Float:  FastPow 2.707 ns vs MathF.Pow 2.508 ns → 7.5 % slower on ARM64;
		//           inject anyway for x86/x64 portability where powf is heavier.
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var method = ParseMethodFromString(paramType.SpecialType == SpecialType.System_Single
				? GenerateFastPowMethodFloat()
				: GenerateFastPowMethodDouble());

			context.AdditionalSyntax.TryAdd(method, false);
			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastPowMethodDouble()
	{
		return """
			private static double FastPow(double x, double y)
			{
				if (Double.IsNaN(x) || Double.IsNaN(y)) return Double.NaN;
				if (y == 0.0 || x == 1.0) return 1.0;
				if (x <= 0.0) return Double.NaN;

				var bits  = BitConverter.DoubleToInt64Bits(x);
				var iexp  = (int)((bits >> 52) & 0x7FF) - 1023;
				var imant = bits & 0x000F_FFFF_FFFF_FFFFL;
				var m     = 1.0 + imant * (1.0 / 4503599627370496.0);  // mantissa ∈ [1, 2)

				// Artanh series for log₂(m): z = (m−1)/(m+1) ∈ [0, 1/3)
				const double INV_LN2 = 1.4426950408889634073599246810018921;
				var z       = (m - 1.0) / (m + 1.0);
				var t2      = z * z;
				var sInner  = Double.FusedMultiplyAdd(1.0 / 7.0, t2, 1.0 / 5.0);
				sInner      = Double.FusedMultiplyAdd(sInner, t2, 1.0 / 3.0);
				sInner      = Double.FusedMultiplyAdd(sInner, t2, 1.0);
				var log2m   = 2.0 * (z * sInner) * INV_LN2;
				var log2x   = iexp + log2m;

				var tv = y * log2x;
				var k  = (long)Double.Round(tv);  // branchless FRINTN + FCVTZS on ARM64
				var f  = tv - k;                   // f ∈ [−0.5, 0.5)

				// Direct degree-7 Horner for 2^f: cₙ = ln(2)ⁿ/n!
				// Saves the intermediate u=LN2·f multiplication vs Taylor e^u approach.
				const double c7 = 1.5253300202639438e-5;  // ln(2)⁷ / 5040
				const double c6 = 1.5403530390456690e-4;  // ln(2)⁶ / 720
				const double c5 = 1.3333558146428443e-3;  // ln(2)⁵ / 120
				const double c4 = 9.6181291076284772e-3;  // ln(2)⁴ / 24
				const double c3 = 5.5504108664821580e-2;  // ln(2)³ / 6
				const double c2 = 2.4022650695910069e-1;  // ln(2)² / 2
				const double c1 = 6.9314718055994531e-1;  // ln(2)

				var p     = Double.FusedMultiplyAdd(c7, f, c6);
				p         = Double.FusedMultiplyAdd(p,  f, c5);
				p         = Double.FusedMultiplyAdd(p,  f, c4);
				p         = Double.FusedMultiplyAdd(p,  f, c3);
				p         = Double.FusedMultiplyAdd(p,  f, c2);
				p         = Double.FusedMultiplyAdd(p,  f, c1);
				var exp2f = Double.FusedMultiplyAdd(p,  f, 1.0);

				return BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52)) * exp2f;
			}
			""";
	}

	private static string GenerateFastPowMethodFloat()
	{
		return """
			private static float FastPow(float x, float y)
			{
				if (Single.IsNaN(x) || Single.IsNaN(y)) return Single.NaN;
				if (y == 0.0f || x == 1.0f) return 1.0f;
				if (x <= 0.0f) return Single.NaN;

				var ibits = BitConverter.SingleToInt32Bits(x);
				var iexp  = ((ibits >> 23) & 0xFF) - 127;
				var imant = ibits & 0x7FFFFF;
				var m     = 1.0f + imant * (1.0f / 8388608.0f);  // mantissa ∈ [1, 2)

				// Artanh series for log₂(m): z = (m−1)/(m+1) ∈ [0, 1/3)
				const float INV_LN2 = 1.4426950408889634f;
				var z       = (m - 1.0f) / (m + 1.0f);
				var t2      = z * z;
				var sInner  = Single.FusedMultiplyAdd(1f / 7f, t2, 1f / 5f);
				sInner      = Single.FusedMultiplyAdd(sInner, t2, 1f / 3f);
				sInner      = Single.FusedMultiplyAdd(sInner, t2, 1f);
				var log2m   = 2.0f * (z * sInner) * INV_LN2;
				var log2x   = iexp + log2m;

				var tv = y * log2x;
				var k  = (int)Single.Round(tv);  // branchless FRINTN + FCVTZS on ARM64
				var f  = tv - k;                  // f ∈ [−0.5, 0.5)

				// Direct degree-5 Horner for 2^f: cₙ = ln(2)ⁿ/n!
				// Float: degree 5 sufficient (artanh log₂ error ~6.5e-6 > exp2 error ~2.3e-6).
				// Saves the intermediate u=LN2·f multiplication + 2 FMAs vs degree-7 Taylor e^u.
				const float c5 = 1.3333558146428443e-3f;  // ln(2)⁵ / 120
				const float c4 = 9.6181291076284772e-3f;  // ln(2)⁴ / 24
				const float c3 = 5.5504108664821580e-2f;  // ln(2)³ / 6
				const float c2 = 2.4022650695910069e-1f;  // ln(2)² / 2
				const float c1 = 6.9314718055994531e-1f;  // ln(2)

				var p     = Single.FusedMultiplyAdd(c5, f, c4);
				p         = Single.FusedMultiplyAdd(p,  f, c3);
				p         = Single.FusedMultiplyAdd(p,  f, c2);
				p         = Single.FusedMultiplyAdd(p,  f, c1);
				var exp2f = Single.FusedMultiplyAdd(p,  f, 1.0f);

				return BitConverter.Int32BitsToSingle((k + 127) << 23) * exp2f;
			}
			""";
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
}
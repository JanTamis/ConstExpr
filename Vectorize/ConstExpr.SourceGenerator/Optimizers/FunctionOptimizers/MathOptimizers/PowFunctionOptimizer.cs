using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

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
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastPowMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastPowMethodDouble(context.FastMathFlags),
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

	private static string GenerateFastPowMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastPow(double x, double y)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x) || Double.IsNaN(y)) return Double.NaN;");
		}

		builder.WriteLine("if (y == 0.0 || x == 1.0) return 1.0;")
			.WriteLine("if (x <= 0.0) return Double.NaN;")
			.WriteWhitespace()
			.WriteLine("var bits  = BitConverter.DoubleToInt64Bits(x);")
			.WriteLine("var iexp  = (int)((bits >> 52) & 0x7FF) - 1023;")
			.WriteLine("var imant = bits & 0x000F_FFFF_FFFF_FFFFL;")
			.WriteLine("var m     = 1.0 + imant * (1.0 / 4503599627370496.0);")
			.WriteWhitespace()
			.WriteLine("var z       = (m - 1.0) / (m + 1.0);")
			.WriteLine("var t2      = z * z;")
			.WriteLine("var sInner  = Double.FusedMultiplyAdd(1.0 / 7.0, t2, 1.0 / 5.0);")
			.WriteLine("sInner      = Double.FusedMultiplyAdd(sInner, t2, 1.0 / 3.0);")
			.WriteLine("sInner      = Double.FusedMultiplyAdd(sInner, t2, 1.0);")
			.WriteLine("var log2m   = 2.0 * (z * sInner) * 1.4426950408889634073599246810018921;")
			.WriteLine("var log2x   = iexp + log2m;")
			.WriteWhitespace()
			.WriteLine("var tv = y * log2x;")
			.WriteLine("var k  = (long)Double.Round(tv);")
			.WriteLine("var f  = tv - k;")
			.WriteWhitespace()
			.WriteLine("var p     = Double.FusedMultiplyAdd(1.5253300202639438e-5, f, 1.5403530390456690e-4);")
			.WriteLine("p         = Double.FusedMultiplyAdd(p,  f, 1.3333558146428443e-3);")
			.WriteLine("p         = Double.FusedMultiplyAdd(p,  f, 9.6181291076284772e-3);")
			.WriteLine("p         = Double.FusedMultiplyAdd(p,  f, 5.5504108664821580e-2);")
			.WriteLine("p         = Double.FusedMultiplyAdd(p,  f, 2.4022650695910069e-1);")
			.WriteLine("p         = Double.FusedMultiplyAdd(p,  f, 6.9314718055994531e-1);")
			.WriteLine("var exp2f = Double.FusedMultiplyAdd(p,  f, 1.0);")
			.WriteWhitespace()
			.WriteLine("return BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52)) * exp2f;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastPowMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastPow(float x, float y)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x) || Single.IsNaN(y)) return Single.NaN;");
		}

		builder.WriteLine("if (y == 0.0f || x == 1.0f) return 1.0f;")
			.WriteLine("if (x <= 0.0f) return Single.NaN;")
			.WriteWhitespace()
			.WriteLine("var ibits = BitConverter.SingleToInt32Bits(x);")
			.WriteLine("var iexp  = ((ibits >> 23) & 0xFF) - 127;")
			.WriteLine("var imant = ibits & 0x7FFFFF;")
			.WriteLine("var m     = 1.0f + imant * (1.0f / 8388608.0f);")
			.WriteWhitespace()
			.WriteLine("var z       = (m - 1.0f) / (m + 1.0f);")
			.WriteLine("var t2      = z * z;")
			.WriteLine("var sInner  = Single.FusedMultiplyAdd(1f / 7f, t2, 1f / 5f);")
			.WriteLine("sInner      = Single.FusedMultiplyAdd(sInner, t2, 1f / 3f);")
			.WriteLine("sInner      = Single.FusedMultiplyAdd(sInner, t2, 1f);")
			.WriteLine("var log2m   = 2.0f * (z * sInner) * 1.4426950408889634f;")
			.WriteLine("var log2x   = iexp + log2m;")
			.WriteWhitespace()
			.WriteLine("var tv = y * log2x;")
			.WriteLine("var k  = (int)Single.Round(tv);")
			.WriteLine("var f  = tv - k;")
			.WriteWhitespace()
			.WriteLine("var p     = Single.FusedMultiplyAdd(1.3333558146428443e-3f, f, 9.6181291076284772e-3f);")
			.WriteLine("p         = Single.FusedMultiplyAdd(p,  f, 5.5504108664821580e-2f);")
			.WriteLine("p         = Single.FusedMultiplyAdd(p,  f, 2.4022650695910069e-1f);")
			.WriteLine("p         = Single.FusedMultiplyAdd(p,  f, 6.9314718055994531e-1f);")
			.WriteLine("var exp2f = Single.FusedMultiplyAdd(p,  f, 1.0f);")
			.WriteWhitespace()
			.WriteLine("return BitConverter.Int32BitsToSingle((k + 127) << 23) * exp2f;");

		builder.EndBlock();

		return builder.ToString();
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
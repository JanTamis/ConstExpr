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

public class AtanhFunctionOptimizer() : BaseMathFunctionOptimizer("Atanh", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var x = context.VisitedParameters[0];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(x, out var value))
		{
			// Atanh(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Atanh(1) => ∞, Atanh(-1) => -∞ (domain boundary)
			if (IsApproximately(Math.Abs(value), 1.0))
			{
				var inf = value > 0
					? paramType.SpecialType == SpecialType.System_Single ? Single.PositiveInfinity : Double.PositiveInfinity
					: paramType.SpecialType == SpecialType.System_Single
						? Single.NegativeInfinity
						: Double.NegativeInfinity;
				result = CreateLiteral(inf.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAtanhMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastAtanhMethodDouble(context.FastMathFlags),
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

	private static string GenerateFastAtanhMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of inverse hyperbolic tangent (Atanh) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a polynomial for |x| &lt; 0.5 and an inline fast-log identity otherwise. ~2.2× faster than Single.Log identity.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value in the open interval (-1, 1).</param>")
			.WriteLine("/// <returns>Approximate inverse hyperbolic tangent value.</returns>")
			.WriteLine("private static float FastAtanh(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("var absX = Single.Abs(x);")
			.WriteWhitespace()
			.WriteLine("if (absX < 0.5f)")
			.StartBlock()
			.WriteLine("var x2 = x * x;")
			.WriteLine("var p = Single.FusedMultiplyAdd(x2, 1f / 7f, 1f / 5f);")
			.WriteLine("p = Single.FusedMultiplyAdd(p, x2, 1f / 3f);")
			.WriteLine("p = Single.FusedMultiplyAdd(p, x2, 1f);")
			.WriteLine("return x * p;")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("var arg  = 1f + 2f * x / (1f - x);")
			.WriteLine("var bits = BitConverter.SingleToInt32Bits(arg);")
			.WriteLine("var e    = (bits >> 23) - 127;")
			.WriteLine("var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);")
			.WriteLine("var lnm  = Single.FusedMultiplyAdd(-0.056570851f, m,  0.447178975f);")
			.WriteLine("lnm      = Single.FusedMultiplyAdd(lnm, m, -1.469956800f);")
			.WriteLine("lnm      = Single.FusedMultiplyAdd(lnm, m,  2.821202636f);")
			.WriteLine("lnm      = Single.FusedMultiplyAdd(lnm, m, -1.741793927f);")
			.WriteLine("return 0.5f * Single.FusedMultiplyAdd(e, 0.6931471806f, lnm);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastAtanhMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of inverse hyperbolic tangent (Atanh) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Polynomial for |x| &lt; 0.5; inline fast-log identity otherwise. ~1.75× faster than Double.Log identity.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value in the open interval (-1, 1).</param>")
			.WriteLine("/// <returns>Approximate inverse hyperbolic tangent value.</returns>")
			.WriteLine("private static double FastAtanh(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (Math.Abs(x) >= 1.0) return x > 0 ? Double.PositiveInfinity : Double.NegativeInfinity;")
			.WriteWhitespace()
			.WriteLine("var absX = Double.Abs(x);")
			.WriteWhitespace()
			.WriteLine("if (absX < 0.5)")
			.StartBlock()
			.WriteLine("var x2 = x * x;")
			.WriteWhitespace()
			.WriteLine("var poly = Double.FusedMultiplyAdd(x2, 1d / 11d, 1d / 9d);")
			.WriteLine("poly = Double.FusedMultiplyAdd(poly, x2, 1d / 7d);")
			.WriteLine("poly = Double.FusedMultiplyAdd(poly, x2, 1d / 5d);")
			.WriteLine("poly = Double.FusedMultiplyAdd(poly, x2, 1d / 3d);")
			.WriteLine("poly = Double.FusedMultiplyAdd(poly, x2, 1d);")
			.WriteWhitespace()
			.WriteLine("return x * poly;")
			.EndBlock()
			.WriteLine("else")
			.StartBlock()
			.WriteLine("var arg  = (1.0 + x) / (1.0 - x);")
			.WriteLine("var bits = BitConverter.DoubleToInt64Bits(arg);")
			.WriteLine("var e    = (int)((bits >> 52) - 1023L);")
			.WriteLine("var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);")
			.WriteLine("var lnm  = Double.FusedMultiplyAdd(-0.056570851, m,  0.447178975);")
			.WriteLine("lnm      = Double.FusedMultiplyAdd(lnm, m, -1.469956800);")
			.WriteLine("lnm      = Double.FusedMultiplyAdd(lnm, m,  2.821202636);")
			.WriteLine("lnm      = Double.FusedMultiplyAdd(lnm, m, -1.741793927);")
			.WriteLine("return 0.5 * Double.FusedMultiplyAdd(e, 0.6931471805599453094172321214581766, lnm);")
			.EndBlock();

		builder.EndBlock();

		return builder.ToString();
	}
}
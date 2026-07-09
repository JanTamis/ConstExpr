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

public class TanPiFunctionOptimizer() : BaseMathFunctionOptimizer("TanPi", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var x = context.VisitedParameters[0];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(x, out var value))
		{
			// TanPi(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// TanPi(0.25) => 1 (tan(?/4) = 1)
			if (IsApproximately(value, 0.25))
			{
				result = CreateLiteral(1.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// TanPi(-0.25) => -1 (tan(-?/4) = -1)
			if (IsApproximately(value, -0.25))
			{
				result = CreateLiteral((-1.0).ToSpecialType(paramType.SpecialType));
				return true;
			}

			// TanPi(0.5) => undefined (asymptote at ?/2), but mathematically approaches infinity
			// TanPi(1.0) => 0 (tan(?) = 0)
			if (IsApproximately(value, 1.0))
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// TanPi(-1.0) => 0 (tan(-?) = 0)
			if (IsApproximately(value, -1.0))
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var method = ParseMethodFromString(paramType.SpecialType == SpecialType.System_Single
				? GenerateFastTanPiMethodFloat(context, paramType)
				: GenerateFastTanPiMethodDouble(context, paramType));

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

	private static string GenerateFastTanPiMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of tangent divided by π (TanPi) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses range reduction and a Padé approximation; values near the asymptote are handled via reciprocal form.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value measured in multiples of π.</param>")
			.WriteLine("/// <returns>Approximate tangent value divided by π.</returns>")
			.WriteLine("private static float FastTanPi(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder
			.WriteLine("x -= Single.Round(x);")
			.WriteWhitespace()
			.WriteLine("var signX = x;")
			.WriteLine("x = Single.Abs(x); // [0, 0.5]")
			.WriteWhitespace()
			.WriteLine("var swap = x > 0.25f;")
			.WriteLine("var xf   = swap ? 0.5f - x : x;")
			.WriteLine("var u2   = xf * xf;")
			.WriteWhitespace()
			.WriteLine($"var num = {multiplyAdd(0.32383247f, "u2", -3.44514185f)};")
			.WriteLine($"num     = {multiplyAdd("num", "u2", "Single.Pi")};")
			.WriteLine("num    *= xf;")
			.WriteLine($"var den = {multiplyAdd(1.54617606f, "u2", -4.38649084f)};")
			.WriteLine($"den     = {multiplyAdd("den", "u2", 1.0f)};")
			.WriteWhitespace()
			.WriteLine("var t = num / den;")
			.WriteLine("if (swap) t = 1.0f / t;")
			.WriteWhitespace()
			.WriteLine("return Single.CopySign(t, signX);")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastTanPiMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of tangent divided by π (TanPi) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses range reduction and a Padé approximation; values near the asymptote are handled via reciprocal form.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value measured in multiples of π.</param>")
			.WriteLine("/// <returns>Approximate tangent value divided by π.</returns>")
			.WriteLine("private static double FastTanPi(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder
			.WriteLine("x -= Double.Round(x);")
			.WriteWhitespace()
			.WriteLine("var signX = x;")
			.WriteLine("x = Double.Abs(x); // [0, 0.5]")
			.WriteWhitespace()
			.WriteLine("var swap = x > 0.25;")
			.WriteLine("var xf   = swap ? 0.5 - x : x;")
			.WriteLine("var u2   = xf * xf;")
			.WriteWhitespace()
			.WriteLine($"var num = {multiplyAdd(0.61822157532380, "u2", -3.75833657307876)};")
			.WriteLine($"num     = {multiplyAdd("num", "u2", "Double.Pi")};")
			.WriteLine("num    *= xf;")
			.WriteLine($"var den = {multiplyAdd(-0.09248641780, "u2", 1.96786042492934)};")
			.WriteLine($"den     = {multiplyAdd("den", "u2", -4.48618381867698)};")
			.WriteLine($"den     = {multiplyAdd("den", "u2", 1.0)};")
			.WriteWhitespace()
			.WriteLine("var t = num / den;")
			.WriteLine("if (swap) t = 1.0 / t;")
			.WriteWhitespace()
			.WriteLine("return Double.CopySign(t, signX);")
			.EndBlock();

		return builder.ToString();
	}
}
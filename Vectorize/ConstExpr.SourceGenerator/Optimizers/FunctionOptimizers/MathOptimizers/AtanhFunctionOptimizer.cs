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

		builder.WriteLine("private static float FastAtanh(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder
			// .WriteLine("// Branchless log1p-style: 0.5 * log(1 + 2x/(1-x))")
			// .WriteLine("// Algebraically equal to 0.5*log((1+x)/(1-x)) but avoids branch overhead.")
			// .WriteLine("// NaN propagates naturally; x=±1 yields ±∞ via log(0)=−∞ and log(+∞)=+∞.")
			.WriteLine("return 0.5f * Single.Log(1f + 2f * x / (1f - x));");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastAtanhMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastAtanh(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (Math.Abs(x) >= 1.0) return x > 0 ? Double.PositiveInfinity : Double.NegativeInfinity;")
			.WriteWhitespace()
			// .WriteLine("// Use the definition: atanh(x) = 0.5 * ln((1 + x) / (1 - x))")
			// .WriteLine("// For small |x|, use Taylor series for better accuracy")
			.WriteLine("var absX = Double.Abs(x);")
			.WriteWhitespace()
			.WriteLine("if (absX < 0.5)")
			.StartBlock()
			// .WriteLine("// Taylor series: atanh(x) = x + x³/3 + x⁵/5 + x⁷/7 + x⁹/9 + x¹¹/11")
			.WriteLine("var x2 = x * x;")
			.WriteWhitespace()
			// .WriteLine("// Horner's context.Method with FMA: x * (1 + x²*(1/3 + x²*(1/5 + x²*(1/7 + x²*(1/9 + x²/11)))))")
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
			// .WriteLine("// Use logarithmic formula: 0.5 * ln((1 + x) / (1 - x))")
			.WriteLine("return 0.5 * Double.Log((1.0 + x) / (1.0 - x));")
			.EndBlock();

		builder.EndBlock();

		return builder.ToString();
	}
}
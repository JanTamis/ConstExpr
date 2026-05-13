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

public class AtanPiFunctionOptimizer() : BaseMathFunctionOptimizer("AtanPi", n => n is 1)
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

		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAtanPiMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastAtanPiMethodDouble(context.FastMathFlags),
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

	private static string GenerateFastAtanPiMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastAtanPi(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("var absX = Single.Abs(x);")
			.WriteLine("var swap = absX > 1.0f;")
			.WriteLine("var a = swap ? 1.0f / absX : absX;")
			.WriteWhitespace()
			// .WriteLine("// A&S §4.4.43 minimax polynomial coefficients pre-divided by π.")
			// .WriteLine("// 4 FMAs + 1 mul. Max absolute error ≈ 3.5e-6 (vs 1.6e-3 for Steinmetz 2-term).")
			.WriteLine("var u = a * a;")
			.WriteLine("var p = Single.FusedMultiplyAdd(u,  0.00663222f, -0.02710107f);")
			.WriteLine("p      = Single.FusedMultiplyAdd(u, p,            0.05733014f);")
			.WriteLine("p      = Single.FusedMultiplyAdd(u, p,           -0.10510700f);")
			.WriteLine("p      = Single.FusedMultiplyAdd(u, p,            0.31826720f);")
			.WriteLine("p     *= a;")
			.WriteWhitespace()
			.WriteLine("p = swap ? 0.5f - p : p;")
			.WriteLine("return Single.IsNegative(x) ? -p : p;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastAtanPiMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastAtanPi(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("var absX = Double.Abs(x);")
			.WriteLine("var swap = absX > 1.0;")
			.WriteLine("var a = swap ? 1.0 / absX : absX;")
			.WriteLine("var u = a * a;")
			.WriteWhitespace()
			// .WriteLine("// A&S §4.4.43 minimax coefficients pre-divided by π — saves the final 1/π multiply.")
			// .WriteLine("// Quadrant correction uses 0.5 (= π/2 / π). Max absolute error ≈ 3.5e-6.")
			.WriteLine("var p = Double.FusedMultiplyAdd(u,  0.00663222, -0.02710107);")
			.WriteLine("p      = Double.FusedMultiplyAdd(u, p,           0.05733014);")
			.WriteLine("p      = Double.FusedMultiplyAdd(u, p,          -0.10510700);")
			.WriteLine("p      = Double.FusedMultiplyAdd(u, p,           0.31826720);")
			.WriteLine("p     *= a;")
			.WriteWhitespace()
			.WriteLine("p = swap ? 0.5 - p : p;")
			.WriteLine("return Double.IsNegative(x) ? -p : p;");

		builder.EndBlock();

		return builder.ToString();
	}
}
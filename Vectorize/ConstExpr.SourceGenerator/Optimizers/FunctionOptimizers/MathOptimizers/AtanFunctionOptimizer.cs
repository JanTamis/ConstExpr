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
			SpecialType.System_Single => GenerateFastAtanMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastAtanMethodDouble(context.FastMathFlags),
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

	private static string GenerateFastAtanMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastAtan(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("var absX = Single.Abs(x);")
			.WriteLine("var swap = absX > 1.0f;")
			.WriteLine("var a = swap ? 1.0f / absX : absX; // exact reciprocal — no ReciprocalEstimate loss")
			.WriteWhitespace()
			.WriteLine("var u = a * a;")
			.WriteLine("var p = Single.FusedMultiplyAdd(u,  0.0208351f, -0.0851330f);")
			.WriteLine("p      = Single.FusedMultiplyAdd(u, p,           0.1801410f);")
			.WriteLine("p      = Single.FusedMultiplyAdd(u, p,          -0.3302995f);")
			.WriteLine("p      = Single.FusedMultiplyAdd(u, p,           0.9998660f);")
			.WriteLine("p     *= a;")
			.WriteWhitespace()
			.WriteLine("p = swap ? Single.Pi / 2 - p : p;")
			.WriteLine("return Single.IsNegative(x) ? -p : p;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastAtanMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastAtan(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("var absX = Double.Abs(x);")
			.WriteLine("var swap = absX > 1.0; ")
			.WriteLine("var a = swap ? 1.0 / absX : absX; // exact reciprocal — no ReciprocalEstimate loss")
			.WriteWhitespace()
			.WriteLine("var u = a * a;")
			.WriteLine("var p = Double.FusedMultiplyAdd(u,  0.0208351, -0.0851330);")
			.WriteLine("p      = Double.FusedMultiplyAdd(u, p,          0.1801410);")
			.WriteLine("p      = Double.FusedMultiplyAdd(u, p,         -0.3302995);")
			.WriteLine("p      = Double.FusedMultiplyAdd(u, p,          0.9998660);")
			.WriteLine("p     *= a;")
			.WriteWhitespace()
			.WriteLine("p = swap ? Double.Pi / 2 - p : p;")
			.WriteLine("return Double.IsNegative(x) ? -p : p;");

		builder.EndBlock();

		return builder.ToString();
	}
}
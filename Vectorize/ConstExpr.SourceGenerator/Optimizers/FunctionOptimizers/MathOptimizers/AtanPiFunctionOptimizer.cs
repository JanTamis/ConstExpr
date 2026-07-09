using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AtanPiFunctionOptimizer() : BaseMathFunctionOptimizer("AtanPi", n => n is 1), IBaseMathCustomImplementation
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

		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAtanPiMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastAtanPiMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return $"{paramType.Name}.{Name}";
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

	private static string GenerateFastAtanPiMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of arctangent divided by π (AtanPi) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses range reduction, a polynomial approximation, and optional NaN handling. Returns atan(x) / π.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate arctangent value divided by π in the range [-0.5, 0.5].</returns>")
			.WriteLine("private static float FastAtanPi(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("var absX = Single.Abs(x);")
			.WriteLine("var swap = absX > 1.0f;")
			.WriteLine("var a = swap ? 1.0f / absX : absX;")
			.WriteWhitespace()
			.WriteLine("var u = a * a;")
			.WriteLine($"var p = {multiplyAdd("u", 0.00663222f, -0.02710107f)};")
			.WriteLine($"p      = {multiplyAdd("u", "p", 0.05733014f)};")
			.WriteLine($"p      = {multiplyAdd("u", "p", -0.10510700f)};")
			.WriteLine($"p      = {multiplyAdd("u", "p", 0.31826720f)};")
			.WriteLine("p     *= a;")
			.WriteWhitespace()
			.WriteLine("p = swap ? 0.5f - p : p;")
			.WriteLine("return Single.IsNegative(x) ? -p : p;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastAtanPiMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of arctangent divided by π (AtanPi) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses range reduction, a polynomial approximation, and optional NaN handling. Returns atan(x) / π.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate arctangent value divided by π in the range [-0.5, 0.5].</returns>")
			.WriteLine("private static double FastAtanPi(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("var absX = Double.Abs(x);")
			.WriteLine("var swap = absX > 1.0;")
			.WriteLine("var a = swap ? 1.0 / absX : absX;")
			.WriteLine("var u = a * a;")
			.WriteWhitespace()
			.WriteLine($"var p = {multiplyAdd("u", 0.00663222, -0.02710107)};")
			.WriteLine($"p      = {multiplyAdd("u", "p", 0.05733014)};")
			.WriteLine($"p      = {multiplyAdd("u", "p", -0.10510700)};")
			.WriteLine($"p      = {multiplyAdd("u", "p", 0.31826720)};")
			.WriteLine("p     *= a;")
			.WriteWhitespace()
			.WriteLine("p = swap ? 0.5 - p : p;")
			.WriteLine("return Double.IsNegative(x) ? -p : p;");

		builder.EndBlock();

		return builder.ToString();
	}
}
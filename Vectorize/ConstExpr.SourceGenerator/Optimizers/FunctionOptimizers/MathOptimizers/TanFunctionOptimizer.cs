using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class TanFunctionOptimizer() : BaseMathFunctionOptimizer("Tan", n => n is 1), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var x = context.VisitedParameters[0];

		// Algebraic simplifications on literal values
		if (TryGetNumericLiteral(x, out var value))
		{
			// Tan(0) => 0
			if (IsApproximately(value, 0.0))
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Tan(π/4) => 1
			if (IsApproximately(value, Math.PI / 4.0))
			{
				result = CreateLiteral(1.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Tan(-π/4) => -1
			if (IsApproximately(value, -Math.PI / 4.0))
			{
				result = CreateLiteral((-1.0).ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Tan(π) => 0
			if (IsApproximately(value, Math.PI))
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}

			// Tan(-π) => 0
			if (IsApproximately(value, -Math.PI))
			{
				result = CreateLiteral(0.0.ToSpecialType(paramType.SpecialType));
				return true;
			}
		}

		if (TryGenerateCustomImplementation(context, paramType, out var method))
		{
			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	public bool TryGenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out MethodDeclarationSyntax? result)
	{
		result = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastTanMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastTanMethodDouble(context, paramType),
			_ => null
		});

		if (result is not null)
		{
			context.AdditionalSyntax.TryAdd(result, false);
			return true;
		}

		return false;
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

	private static string GenerateFastTanMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of tangent (Tan) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses range reduction and a rational approximation, with a reciprocal form near the asymptote.</remarks>")
			.WriteLine("/// <param name=\"x\">Input angle in radians.</param>")
			.WriteLine("/// <returns>Approximate tangent value.</returns>")
			.WriteLine("private static float FastTan(float x)")
			.StartBlock()
			.WriteLine("if (Single.IsNaN(x)) return Single.NaN;")
			.WriteWhitespace()
			.WriteLine("const float InvPi  = 1.0f / Single.Pi;")
			.WriteLine("const float HalfPi = Single.Pi * 0.5f;")
			.WriteWhitespace()
			.WriteLine("// Range reduce to (−π/2, π/2) — tan's period is π")
			.WriteLine("var quotient = Single.Round(x * InvPi);")
			.WriteLine($"var xReduced = {multiplyAdd("-quotient", "Single.Pi", "x")};")
			.WriteWhitespace()
			.WriteLine("var absX          = Single.Abs(xReduced);")
			.WriteLine("var nearAsymptote = absX > 1.4f;")
			.WriteWhitespace()
			.WriteLine("var arg = nearAsymptote ? HalfPi - absX : xReduced;")
			.WriteWhitespace()
			.WriteLine("var x2 = arg * arg;")
			.WriteWhitespace()
			.WriteLine("var p1 = -0.1306282f;")
			.WriteLine("var p2 =  0.0052854f;")
			.WriteLine($"var num = {multiplyAdd("p2", "x2", "p1")};")
			.WriteLine($"num      = {multiplyAdd("num", "x2", 1.0f)};")
			.WriteLine("num     *= arg;")
			.WriteWhitespace()
			.WriteLine("var q1 = -0.4636476f;")
			.WriteLine("var q2 =  0.0157903f;")
			.WriteLine($"var den = {multiplyAdd("q2", "x2", "q1")};")
			.WriteLine($"den      = {multiplyAdd("den", "x2", 1.0f)};")
			.WriteWhitespace()
			.WriteLine("if (nearAsymptote)")
			.StartBlock()
			.WriteLine("return Single.CopySign(den / num, xReduced);")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("return num / den;")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastTanMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of tangent (Tan) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses range reduction and a rational approximation, with a reciprocal form near the asymptote.</remarks>")
			.WriteLine("/// <param name=\"x\">Input angle in radians.</param>")
			.WriteLine("/// <returns>Approximate tangent value.</returns>")
			.WriteLine("private static double FastTan(double x)")
			.StartBlock()
			.WriteLine("if (Double.IsNaN(x)) return Double.NaN;")
			.WriteWhitespace()
			.WriteLine("const double InvPi  = 1.0 / Double.Pi;")
			.WriteLine("const double HalfPi = Double.Pi * 0.5;")
			.WriteWhitespace()
			.WriteLine("var quotient = Double.Round(x * InvPi);")
			.WriteLine($"var xReduced = {multiplyAdd("-quotient", "Double.Pi", "x")};")
			.WriteWhitespace()
			.WriteLine("var absX          = Double.Abs(xReduced);")
			.WriteLine("var nearAsymptote = absX > 1.4;")
			.WriteWhitespace()
			.WriteLine("var arg = nearAsymptote ? HalfPi - absX : xReduced;")
			.WriteWhitespace()
			.WriteLine("var x2 = arg * arg;")
			.WriteWhitespace()
			.WriteLine("var p1 = -0.13089944486966634;")
			.WriteLine("var p2 =  0.005405742881796775;")
			.WriteLine("var p3 = -0.00010606776596208569;")
			.WriteLine($"var num = {multiplyAdd("p3", "x2", "p2")};")
			.WriteLine($"num      = {multiplyAdd("num", "x2", "p1")};")
			.WriteLine($"num      = {multiplyAdd("num", "x2", 1.0)};")
			.WriteLine("num     *= arg;")
			.WriteWhitespace()
			.WriteLine("var q1 = -0.46468849716162905;")
			.WriteLine("var q2 =  0.015893657956882884;")
			.WriteLine("var q3 = -0.00031920703894961204;")
			.WriteLine($"var den = {multiplyAdd("q3", "x2", "q2")};")
			.WriteLine($"den      = {multiplyAdd("den", "x2", "q1")};")
			.WriteLine($"den      = {multiplyAdd("den", "x2", 1.0)};")
			.WriteWhitespace()
			.WriteLine("if (nearAsymptote)")
			.StartBlock()
			.WriteLine("return Double.CopySign(den / num, xReduced);")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("return num / den;")
			.EndBlock();

		return builder.ToString();
	}
}
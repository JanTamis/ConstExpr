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

		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastTanMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastTanMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
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

		var roundInvocation = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);
		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of tangent (Tan) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses range reduction and a rational approximation, with a reciprocal form near the asymptote.</remarks>")
			.WriteLine("/// <param name=\"x\">Input angle in radians.</param>")
			.WriteLine("/// <returns>Approximate tangent value.</returns>")
			.WriteLine("private static float FastTan(float x)")
			.StartBlock()
			.WriteLine("if (Single.IsNaN(x)) return Single.NaN;")
			.WriteWhitespace()
			.WriteLine($"var quotient = {roundInvocation}(x * (1.0f / Single.Pi));")
			.WriteLine($"var xReduced = {multiplyAdd("-quotient", "Single.Pi", "x")};")
			.WriteWhitespace()
			.WriteLine($"var absX          = {absInvocation}(xReduced);")
			.WriteLine("var nearAsymptote = absX > 1.4f;")
			.WriteWhitespace()
			.WriteLine("var arg = nearAsymptote ? Single.Pi * 0.5f - absX : xReduced;")
			.WriteWhitespace()
			.WriteLine("var x2 = arg * arg;")
			.WriteWhitespace()
			.WriteLine($"var num = {multiplyAdd(0.0052854f, "x2", -0.1306282f)};")
			.WriteLine($"num      = {multiplyAdd("num", "x2", 1.0f)};")
			.WriteLine("num     *= arg;")
			.WriteWhitespace()
			.WriteLine($"var den = {multiplyAdd(0.0157903f, "x2", -0.4636476f)};")
			.WriteLine($"den      = {multiplyAdd("den", "x2", 1.0f)};")
			.WriteWhitespace()
			.WriteLine("if (nearAsymptote)")
			.StartBlock()
			.WriteLine($"return {copySignInvocation}(den / num, xReduced);")
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

		var roundInvocation = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);
		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);

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
			.WriteLine($"var quotient = {roundInvocation}(x * InvPi);")
			.WriteLine($"var xReduced = {multiplyAdd("-quotient", "Double.Pi", "x")};")
			.WriteWhitespace()
			.WriteLine($"var absX          = {absInvocation}(xReduced);")
			.WriteLine("var nearAsymptote = absX > 1.4;")
			.WriteWhitespace()
			.WriteLine("var arg = nearAsymptote ? HalfPi - absX : xReduced;")
			.WriteWhitespace()
			.WriteLine("var x2 = arg * arg;")
			.WriteWhitespace()
			.WriteLine($"var num = {multiplyAdd(-0.00010606776596208569, "x2", 0.005405742881796775)};")
			.WriteLine($"num      = {multiplyAdd("num", "x2", -0.13089944486966634)};")
			.WriteLine($"num      = {multiplyAdd("num", "x2", 1.0)};")
			.WriteLine("num     *= arg;")
			.WriteWhitespace()
			.WriteLine($"var den = {multiplyAdd(-0.00031920703894961204, "x2", 0.015893657956882884)};")
			.WriteLine($"den      = {multiplyAdd("den", "x2", -0.46468849716162905)};")
			.WriteLine($"den      = {multiplyAdd("den", "x2", 1.0)};")
			.WriteWhitespace()
			.WriteLine("if (nearAsymptote)")
			.StartBlock()
			.WriteLine($"return {copySignInvocation}(den / num, xReduced);")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("return num / den;")
			.EndBlock();

		return builder.ToString();
	}
}
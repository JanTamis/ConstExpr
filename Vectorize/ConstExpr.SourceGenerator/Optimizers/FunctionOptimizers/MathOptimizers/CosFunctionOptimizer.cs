using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CosFunctionOptimizer() : BaseMathFunctionOptimizer("Cos", n => n is 1), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
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
			SpecialType.System_Single => GenerateFastCosMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastCosMethodDouble(context, paramType),
			_ => null
		});

		if (result is not null)
		{
			context.AdditionalSyntax.TryAdd(result, false);
			return true;
		}

		return false;
	}

	private static string GenerateFastCosMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of cosine (Cos) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses argument reduction and a polynomial approximation with optional NaN handling.</remarks>")
			.WriteLine("/// <param name=\"x\">Input angle in radians.</param>")
			.WriteLine("/// <returns>Approximate cosine value.</returns>")
			.WriteLine("private static float FastCos(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("x -= Single.Round(x * (1f / Single.Tau)) * Single.Tau;")
			.WriteWhitespace()
			.WriteLine("x = Single.Abs(x);")
			.WriteWhitespace()
			.WriteLine("var x2 = x * x;")
			.WriteLine("var ret = 0.0003538394f;")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.0041666418f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 0.041666666f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.5f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 1.0f)};")
			.WriteWhitespace()
			.WriteLine("return ret;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastCosMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of cosine (Cos) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses argument reduction and a polynomial approximation with optional NaN handling.</remarks>")
			.WriteLine("/// <param name=\"x\">Input angle in radians.</param>")
			.WriteLine("/// <returns>Approximate cosine value.</returns>")
			.WriteLine("private static double FastCos(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("x -= Double.Round(x * (1.0 / Double.Tau)) * Double.Tau;")
			.WriteWhitespace()
			.WriteLine("x = Double.Abs(x);")
			.WriteWhitespace()
			.WriteLine("var x2 = x * x;")
			.WriteLine("var ret = -1.1940250944959890e-7;")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 2.0876755527587203e-5)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.0013888888888739916)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 0.041666666666666602)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.5)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 1.0)};")
			.WriteWhitespace()
			.WriteLine("return ret;");

		builder.EndBlock();

		return builder.ToString();
	}
}
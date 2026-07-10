using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CosFunctionOptimizer() : BaseMathFunctionOptimizer("Cos", n => n is 1), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastCosMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastCosMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
	}

	private static string GenerateFastCosMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var roundInvocation = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);

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

		builder.WriteLine($"x -= {roundInvocation}(x * (1f / Single.Tau)) * Single.Tau;")
			.WriteWhitespace()
			.WriteLine($"x = {absInvocation}(x);")
			.WriteWhitespace()
			.WriteLine("var x2 = x * x;")
			.WriteLine($"var ret = {multiplyAdd(0.0003538394f, "x2", -0.0041666418f)};")
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

		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var roundInvocation = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);

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

		builder.WriteLine($"x -= {roundInvocation}(x * (1.0 / Double.Tau)) * Double.Tau;")
			.WriteWhitespace()
			.WriteLine($"x = {absInvocation}(x);")
			.WriteWhitespace()
			.WriteLine("var x2 = x * x;")
			.WriteLine($"var ret = {multiplyAdd(-1.1940250944959890e-7, "x2", 2.0876755527587203e-5)};")
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
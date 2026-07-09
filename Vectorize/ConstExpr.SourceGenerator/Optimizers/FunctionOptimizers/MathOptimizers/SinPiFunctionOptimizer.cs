using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SinPiFunctionOptimizer() : BaseMathFunctionOptimizer("SinPi", n => n is 1), IBaseMathCustomImplementation
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
			SpecialType.System_Single => GenerateFastSinPiMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastSinPiMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
	}

	private static string GenerateFastSinPiMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var roundInvocation = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);
		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var minInvocation = GetMethodInvocation<MinFunctionOptimizer>(context, paramType);
		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of sine divided by π (SinPi) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses range reduction modulo 2 and a polynomial approximation for sin(πx). Returns sin(πx).</remarks>")
			.WriteLine("/// <param name=\"x\">Input value measured in multiples of π.</param>")
			.WriteLine("/// <returns>Approximate sine value.</returns>")
			.WriteLine("private static float FastSinPi(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteWhitespace()
			.WriteLine($"x -= {roundInvocation}(x * 0.5f) * 2.0f;")
			.WriteLine("var sign = x;")
			.WriteLine($"x = {absInvocation}<float, uint>(x);")
			.WriteWhitespace()
			.WriteLine($"var u  = {minInvocation}(x, 1.0f - x);")
			.WriteLine("var u2 = u * u;")
			.WriteWhitespace()
			.WriteLine($"var r = {multiplyAdd(-0.59926453f, "u2", 2.55016404f)};")
			.WriteLine($"r = {multiplyAdd("r", "u2", -5.16771278f)};")
			.WriteLine($"r = {multiplyAdd("r", "u2", 3.14159265f)};")
			.WriteLine($"return {copySignInvocation}(u * r, sign);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastSinPiMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		var roundInvocation = GetMethodInvocation<RoundFunctionOptimizer>(context, paramType);
		var absInvocation = GetMethodInvocation<AbsFunctionOptimizer>(context, paramType);
		var minInvocation = GetMethodInvocation<MinFunctionOptimizer>(context, paramType);
		var copySignInvocation = GetMethodInvocation<CopySignFunctionOptimizer>(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of sine divided by π (SinPi) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses range reduction modulo 2 and a polynomial approximation for sin(πx). Returns sin(πx).</remarks>")
			.WriteLine("/// <param name=\"x\">Input value measured in multiples of π.</param>")
			.WriteLine("/// <returns>Approximate sine value.</returns>")
			.WriteLine("private static double FastSinPi(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteWhitespace()
			.WriteLine($"x -= {roundInvocation}(x * 0.5) * 2.0;")
			.WriteLine("var sign = x;")
			.WriteLine($"x = {absInvocation}<double, ulong>(x);")
			.WriteWhitespace()
			.WriteLine($"var u  = {minInvocation}(x, 1.0 - x);")
			.WriteLine("var u2 = u * u;")
			.WriteWhitespace()
			.WriteLine($"var r = {multiplyAdd(0.08214588661112823, "u2", -0.59926452932079209)};")
			.WriteLine($"r = {multiplyAdd("r", "u2", 2.55016403987734485)};")
			.WriteLine($"r = {multiplyAdd("r", "u2", -5.16771278004997102)};")
			.WriteLine($"r = {multiplyAdd("r", "u2", 3.14159265358979324)};")
			.WriteLine($"return {copySignInvocation}(u * r, sign);");

		builder.EndBlock();

		return builder.ToString();
	}
}
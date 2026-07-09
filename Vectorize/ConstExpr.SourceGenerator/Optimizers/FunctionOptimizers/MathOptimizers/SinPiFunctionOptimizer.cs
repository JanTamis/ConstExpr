using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SinPiFunctionOptimizer() : BaseMathFunctionOptimizer("SinPi", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
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

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastSinPiMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

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
			.WriteLine("x -= Single.Round(x * 0.5f) * 2.0f;")
			.WriteLine("var sign = x;")
			.WriteLine("x = Single.Abs(x);")
			.WriteWhitespace()
			.WriteLine("var u  = Single.Min(x, 1.0f - x);")
			.WriteLine("var u2 = u * u;")
			.WriteWhitespace()
			.WriteLine("var r = -0.59926453f")
			.WriteLine($"r = {multiplyAdd("r", "u2", 2.55016404f)};")
			.WriteLine($"r = {multiplyAdd("r", "u2", -5.16771278f)};")
			.WriteLine($"r = {multiplyAdd("r", "u2", 3.14159265f)};")
			.WriteLine("return Single.CopySign(u * r, sign);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastSinPiMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

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
			.WriteLine("x -= Double.Round(x * 0.5) * 2.0;")
			.WriteLine("var sign = x;")
			.WriteLine("x = Double.Abs(x);")
			.WriteWhitespace()
			.WriteLine("var u  = Double.Min(x, 1.0 - x);")
			.WriteLine("var u2 = u * u;")
			.WriteWhitespace()
			.WriteLine("var r =  0.08214588661112823;")
			.WriteLine($"r = {multiplyAdd("r", "u2", -0.59926452932079209)};")
			.WriteLine($"r = {multiplyAdd("r", "u2", 2.55016403987734485)};")
			.WriteLine($"r = {multiplyAdd("r", "u2", -5.16771278004997102)};")
			.WriteLine($"r = {multiplyAdd("r", "u2", 3.14159265358979324)};")
			.WriteLine("return Double.CopySign(u * r, sign);");

		builder.EndBlock();

		return builder.ToString();
	}
}
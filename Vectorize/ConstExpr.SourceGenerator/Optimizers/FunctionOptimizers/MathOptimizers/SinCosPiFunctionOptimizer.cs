using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SinCosPiFunctionOptimizer() : BaseMathFunctionOptimizer("SinCosPi", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastSinCosPiMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastSinCosPiMethodDouble(context, paramType),
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

	private static string GenerateFastSinCosPiMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast simultaneous sine and cosine approximation for values interpreted as multiples of π (single-precision).</summary>")
			.WriteLine("/// <remarks>Uses range reduction modulo 2 and paired polynomial approximations for sin(πx) and cos(πx).</remarks>")
			.WriteLine("/// <param name=\"x\">Input value measured in multiples of π.</param>")
			.WriteLine("/// <returns>A tuple containing approximate sine and cosine values.</returns>")
			.WriteLine("private static (float Sin, float Cos) FastSinCosPi(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return (Single.NaN, Single.NaN);");
		}

		builder.WriteWhitespace()
			.WriteLine("x -= Single.Round(x * 0.5f) * 2.0f;")
			.WriteWhitespace()
			.WriteLine("var originalSign = x;")
			.WriteLine("var absX = Single.Abs(x);")
			.WriteWhitespace()
			.WriteLine("var inUpperHalf = absX > 0.5f;")
			.WriteLine("var u = inUpperHalf ? (1.0f - absX) : absX;")
			.WriteWhitespace()
			.WriteLine("var u2 = u * u;")
			.WriteWhitespace()
			.WriteLine("var sinVal = -0.5992645f;")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "u2", 2.5501640f)};")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "u2", -5.1677128f)};")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "u2", 3.1415927f)};")
			.WriteLine("sinVal = sinVal * u;")
			.WriteWhitespace()
			.WriteLine("sinVal = Single.CopySign(sinVal, originalSign);")
			.WriteWhitespace()
			.WriteLine("var cosVal = -1.3352627f;")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "u2", 4.0587121f)};")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "u2", -4.9348022f)};")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "u2", 1.0f)};")
			.WriteWhitespace()
			.WriteLine("cosVal = inUpperHalf ? -cosVal : cosVal;")
			.WriteWhitespace()
			.WriteLine("return (sinVal, cosVal);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastSinCosPiMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast simultaneous sine and cosine approximation for values interpreted as multiples of π (double-precision).</summary>")
			.WriteLine("/// <remarks>Uses range reduction modulo 2 and paired polynomial approximations for sin(πx) and cos(πx).</remarks>")
			.WriteLine("/// <param name=\"x\">Input value measured in multiples of π.</param>")
			.WriteLine("/// <returns>A tuple containing approximate sine and cosine values.</returns>")
			.WriteLine("private static (double Sin, double Cos) FastSinCosPi(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return (Double.NaN, Double.NaN);");
		}

		builder.WriteWhitespace()
			.WriteLine("x -= Double.Round(x * 0.5) * 2.0;")
			.WriteWhitespace()
			.WriteLine("var originalSign = x;")
			.WriteLine("var absX = Double.Abs(x);")
			.WriteWhitespace()
			.WriteLine("var inUpperHalf = absX > 0.5;")
			.WriteLine("var u = inUpperHalf ? (1.0 - absX) : absX;")
			.WriteWhitespace()
			.WriteLine("var u2 = u * u;")
			.WriteWhitespace()
			.WriteLine("var sinVal = 0.08214588661112823;")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "u2", -0.5992645293218801)};")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "u2", 2.5501640398773455)};")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "u2", -5.1677127800499706)};")
			.WriteLine($"sinVal = {multiplyAdd("sinVal", "u2", 3.1415926535897932)};")
			.WriteLine("sinVal = sinVal * u;")
			.WriteWhitespace()
			.WriteLine("sinVal = Double.CopySign(sinVal, originalSign);")
			.WriteWhitespace()
			.WriteLine("var cosVal = 0.23533075157732439;")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "u2", -1.3352627312227247)};")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "u2", 4.0587121264167682)};")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "u2", -4.9348022005446793)};")
			.WriteLine($"cosVal = {multiplyAdd("cosVal", "u2", 1.0)};")
			.WriteWhitespace()
			.WriteLine("cosVal = inUpperHalf ? -cosVal : cosVal;")
			.WriteWhitespace()
			.WriteLine("return (sinVal, cosVal);");

		builder.EndBlock();

		return builder.ToString();
	}
}
using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SinFunctionOptimizer() : BaseMathFunctionOptimizer("Sin", n => n is 1), IBaseMathCustomImplementation
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
			SpecialType.System_Single => GenerateFastSinMethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastSinMethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return $"{paramType.Name}.{Name}";
	}

	private static string GenerateFastSinMethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of sine (Sin) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses argument reduction and a polynomial approximation with optional NaN handling.</remarks>")
			.WriteLine("/// <param name=\"x\">Input angle in radians.</param>")
			.WriteLine("/// <returns>Approximate sine value.</returns>")
			.WriteLine("private static float FastSin(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteWhitespace()
			.WriteLine("var originalX = x;")
			.WriteWhitespace()
			.WriteLine("x -= Single.Round(x * (1.0f / Single.Tau)) * Single.Tau;")
			.WriteWhitespace()
			.WriteLine("x = Single.Abs(x);")
			.WriteLine("x = Single.Min(x, Single.Pi - x);")
			.WriteWhitespace()
			.WriteLine("var x2 = x * x;")
			.WriteLine("var ret = -1.9841269841e-4f;")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 8.3333333333e-3f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -1.6666666667e-1f)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 1.0f)};")
			.WriteLine("ret *= x;")
			.WriteWhitespace()
			.WriteLine("return Single.CopySign(ret, originalX);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastSinMethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of sine (Sin) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses argument reduction and a polynomial approximation with optional NaN handling.</remarks>")
			.WriteLine("/// <param name=\"x\">Input angle in radians.</param>")
			.WriteLine("/// <returns>Approximate sine value.</returns>")
			.WriteLine("private static double FastSin(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteWhitespace()
			.WriteLine("var originalX = x;")
			.WriteWhitespace()
			.WriteLine("x -= Double.Round(x * (1.0 / Double.Tau)) * Double.Tau;")
			.WriteWhitespace()
			.WriteLine("x = Double.Abs(x);")
			.WriteWhitespace()
			.WriteLine("if (x > Double.Pi / 2.0)")
			.StartBlock()
			.WriteLine("x = Double.Pi - x;")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("var x2 = x * x;")
			.WriteLine("var ret = 2.6019406621361745e-9;")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -1.9839531932589676e-7)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 8.3333333333216515e-6)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.00019841269836761127)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 0.0083333333333332177)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", -0.16666666666666666)};")
			.WriteLine($"ret = {multiplyAdd("ret", "x2", 1.0)};")
			.WriteLine("ret *= x;")
			.WriteWhitespace()
			// .WriteLine("// Apply original sign using CopySign")
			.WriteLine("return Double.CopySign(ret, originalX);");

		builder.EndBlock();

		return builder.ToString();
	}
}
using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Log10FunctionOptimizer() : BaseMathFunctionOptimizer("Log10", n => n is 1), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Log10(Exp10(x)) => x  (inverse-operation cancellation)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Exp10" }, ArgumentList.Arguments.Count: 1 } inv
		    && IsPure(inv.ArgumentList.Arguments[0].Expression))
		{
			result = inv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		// For float / double: replace with a scalar fast polynomial approximation.
		// FastLog10V2 uses a degree-4 Horner polynomial whose coefficients are the
		// ln(m) minimax coefficients pre-multiplied by log10(e), eliminating the
		// final lnm * LOG10_E multiply.  Benchmark speedup vs Math.Log10:
		//   float  ≈ 2.0×  (1.782 ns → 0.897 ns, Apple M4 Pro / ARM64 RyuJIT)
		//   double ≈ 2.3×  (2.020 ns → 0.892 ns)
		// Max relative error ≈ 8.7e-5 (fast-math trade-off).
		result = CreateInvocation(GenerateCustomImplementation(context, paramType), context.VisitedParameters);
		return true;
	}

	public override string GenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastLog10MethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastLog10MethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return $"{paramType.Name}.{Name}";
	}

	private static string GenerateFastLog10MethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of base-10 logarithm (Log10) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses exponent extraction and a polynomial approximation for the mantissa. Returns log10(x).</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate base-10 logarithm of x.</returns>")
			.WriteLine("private static float FastLog10(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x) || x < 0f) return Single.NaN;");
		}

		builder.WriteLine("if (x == 0f) return Single.NegativeInfinity;");

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;");
		}

		builder.WriteWhitespace()
			.WriteLine("var bits = BitConverter.SingleToInt32Bits(x);")
			.WriteLine("var e    = (bits >> 23) - 127;")
			.WriteLine("var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);")
			.WriteWhitespace()
			.WriteLine($"var log10m = {multiplyAdd(-0.024568408f, "m", 0.194207361f)};")
			.WriteLine($"log10m     = {multiplyAdd("log10m", "m", -0.638394127f)};")
			.WriteLine($"log10m     = {multiplyAdd("log10m", "m", 1.225232737f)};")
			.WriteLine($"log10m     = {multiplyAdd("log10m", "m", -0.756451491f)};")
			.WriteWhitespace()
			.WriteLine($"return {multiplyAdd("e", 0.30102999566398120f, "log10m")};");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastLog10MethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of base-10 logarithm (Log10) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses exponent extraction and a polynomial approximation for the mantissa. Returns log10(x).</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate base-10 logarithm of x.</returns>")
			.WriteLine("private static double FastLog10(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x) || x < 0.0) return Double.NaN;");
		}

		builder.WriteLine("if (x == 0.0) return Double.NegativeInfinity;");

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;");
		}

		builder.WriteWhitespace()
			.WriteLine("var bits = BitConverter.DoubleToInt64Bits(x);")
			.WriteLine("var e    = (int)((bits >> 52) - 1023L);")
			.WriteLine("var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);")
			.WriteWhitespace()
			.WriteLine($"var log10m = {multiplyAdd(-0.024568408426, "m", 0.194207361266)};")
			.WriteLine($"log10m     = {multiplyAdd("log10m", "m", -0.638394126876)};")
			.WriteLine($"log10m     = {multiplyAdd("log10m", "m", 1.225232737146)};")
			.WriteLine($"log10m     = {multiplyAdd("log10m", "m", -0.756451491109)};")
			.WriteWhitespace()
			.WriteLine($"return {multiplyAdd("e", 0.30102999566398119521373889472449303, "log10m")};");

		builder.EndBlock();

		return builder.ToString();
	}
}
using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Exp10FunctionOptimizer() : BaseMathFunctionOptimizer("Exp10", n => n is 1), IBaseMathCustomImplementation
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
			SpecialType.System_Single => GenerateFastExp10MethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastExp10MethodDouble(context, paramType),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);
			return method.Identifier.Text;
		}

		return base.GenerateCustomImplementation(context, paramType);
	}

	private static string GenerateFastExp10MethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of base-10 exponential (Exp10) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a base-2 reduction via log₂(10) and a polynomial approximation. Clamps at ±overflow bounds.</remarks>")
			.WriteLine("/// <param name=\"x\">Input exponent value.</param>")
			.WriteLine("/// <returns>Approximate value of 10^x.</returns>")
			.WriteLine("private static float FastExp10(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;")
				.WriteLine("if (Single.IsNegativeInfinity(x)) return 0.0f;");
		}

		builder.WriteLine("if (x == 0.0f) return 1.0f; // handles +0 and -0")
			.WriteWhitespace()
			.WriteLine("if (x >= 38.53f) return Single.PositiveInfinity;")
			.WriteLine("if (x <= -38.53f) return 0.0f;")
			.WriteWhitespace()
			.WriteLine("var kf = x * 3.321928094887362f;")
			.WriteLine("var k  = (int)(kf + (kf >= 0.0f ? 0.5f : -0.5f));")
			.WriteLine($"var r  = {multiplyAdd("-k", 0.30102999566398120f, "x")};")
			.WriteWhitespace()
			.WriteLine($"var poly = {multiplyAdd(1.1712551f, "r", 1.1712551f)};")
			.WriteLine($"poly = {multiplyAdd("poly", "r", 2.6509491f)};")
			.WriteLine($"poly = {multiplyAdd("poly", "r", 2.3025851f)};")
			.WriteLine($"var expR = {multiplyAdd("poly", "r", 1.0f)};")
			.WriteWhitespace()
			.WriteLine("var bits = (k + 127) << 23;")
			.WriteLine("var scale = BitConverter.Int32BitsToSingle(bits);")
			.WriteLine("return scale * expR;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastExp10MethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of base-10 exponential (Exp10) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a base-2 reduction via log₂(10) and a polynomial approximation. Clamps at ±overflow bounds.</remarks>")
			.WriteLine("/// <param name=\"x\">Input exponent value.</param>")
			.WriteLine("/// <returns>Approximate value of 10^x.</returns>")
			.WriteLine("private static double FastExp10(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;")
				.WriteLine("if (Double.IsNegativeInfinity(x)) return 0.0;");
		}

		builder.WriteLine("if (x == 0.0) return 1.0; // handles +0 and -0")
			.WriteWhitespace()
			.WriteLine("if (x >= 309.0) return Double.PositiveInfinity;")
			.WriteLine("if (x <= -309.0) return 0.0;")
			.WriteWhitespace()
			.WriteLine("var kf = x * 3.321928094887362347870319429489390;")
			.WriteLine("var k  = (long)(kf + (kf >= 0.0 ? 0.5 : -0.5));")
			.WriteLine($"var r  = {multiplyAdd("-k", 0.30102999566398119521373889472449303, "x")};")
			.WriteWhitespace()
			.WriteLine($"var poly = {multiplyAdd(1.1712551489122673, "r", 2.0346785922934770)};")
			.WriteLine($"poly = {multiplyAdd("poly", "r", 2.6509490552391997)};")
			.WriteLine($"poly = {multiplyAdd("poly", "r", 2.302585092994046)};")
			.WriteLine($"var expR = {multiplyAdd("poly", "r", 1.0)};")
			.WriteWhitespace()
			.WriteLine("var bits = (ulong)((k + 1023L) << 52);")
			.WriteLine("var scale = BitConverter.UInt64BitsToDouble(bits);")
			.WriteLine("return scale * expR;");

		builder.EndBlock();

		return builder.ToString();
	}
}
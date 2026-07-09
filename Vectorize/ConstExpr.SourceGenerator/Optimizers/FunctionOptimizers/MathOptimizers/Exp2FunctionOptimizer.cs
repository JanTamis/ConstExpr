using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Interfaces;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Exp2FunctionOptimizer() : BaseMathFunctionOptimizer("Exp2", n => n is 1), IBaseMathCustomImplementation
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryGenerateCustomImplementation(context, paramType, out var method))
		{
			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: keep as Exp2 call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	public bool TryGenerateCustomImplementation(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out MethodDeclarationSyntax? result)
	{
		result = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastExp2MethodFloat(context, paramType),
			SpecialType.System_Double => GenerateFastExp2MethodDouble(context, paramType),
			_ => null
		});

		if (result is not null)
		{
			context.AdditionalSyntax.TryAdd(result, false);
			return true;
		}

		return false;
	}

	private static string GenerateFastExp2MethodFloat(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of base-2 exponential (Exp2) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses integer exponent extraction and a polynomial approximation. Clamps at ±overflow bounds.</remarks>")
			.WriteLine("/// <param name=\"x\">Input exponent value.</param>")
			.WriteLine("/// <returns>Approximate value of 2^x.</returns>")
			.WriteLine("private static float FastExp2(float x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;");
		}

		builder.WriteLine("if (x >= 128.0f) return float.PositiveInfinity;")
			.WriteLine("if (x < -150.0f) return 0.0f;")
			.WriteWhitespace()
			.WriteLine("var k = (int)(x + (x >= 0.0f ? 0.5f : -0.5f));")
			.WriteLine("var r = x - k;")
			.WriteWhitespace()
			.WriteLine($"var p    = {multiplyAdd(0.009618129f, "r", 0.055504109f)};")
			.WriteLine($"p        = {multiplyAdd("p", "r", 0.240226507f)};")
			.WriteLine($"p        = {multiplyAdd("p", "r", 0.693147181f)};")
			.WriteLine($"var expR = {multiplyAdd("p", "r", 1.0f)};")
			.WriteWhitespace()
			.WriteLine("var bits = (k + 127) << 23;")
			.WriteLine("var scale = BitConverter.Int32BitsToSingle(bits);")
			.WriteLine("return scale * expR;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastExp2MethodDouble(FunctionOptimizerContext context, ITypeSymbol paramType)
	{
		var builder = new CodeWriter();
		var multiplyAdd = MultiplyAddEstimate(context, paramType);

		builder.WriteLine("/// <summary>Fast approximation of base-2 exponential (Exp2) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses integer exponent extraction and a polynomial approximation. Clamps at ±overflow bounds.</remarks>")
			.WriteLine("/// <param name=\"x\">Input exponent value.</param>")
			.WriteLine("/// <returns>Approximate value of 2^x.</returns>")
			.WriteLine("private static double FastExp2(double x)")
			.StartBlock();

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		if (!context.FastMathFlags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;");
		}

		builder.WriteLine("if (x >= 1024.0) return Double.PositiveInfinity;")
			.WriteLine("if (x < -1100.0) return 0.0;")
			.WriteWhitespace()
			.WriteLine("var k = (long)(x + (x >= 0.0 ? 0.5 : -0.5));")
			.WriteLine("var r = x - k;")
			.WriteWhitespace()
			.WriteLine($"var p    = {multiplyAdd(9.618129107628477e-3, "r", 5.550410866482158e-2)};")
			.WriteLine($"p        = {multiplyAdd("p", "r", 2.402265069591007e-1)};")
			.WriteLine($"p        = {multiplyAdd("p", "r", 6.931471805599453e-1)};")
			.WriteLine($"var expR = {multiplyAdd("p", "r", 1.0)};")
			.WriteWhitespace()
			.WriteLine("var bits = (ulong)((k + 1023L) << 52);")
			.WriteLine("var scale = BitConverter.UInt64BitsToDouble(bits);")
			.WriteLine("return scale * expR;");

		builder.EndBlock();

		return builder.ToString();
	}
}
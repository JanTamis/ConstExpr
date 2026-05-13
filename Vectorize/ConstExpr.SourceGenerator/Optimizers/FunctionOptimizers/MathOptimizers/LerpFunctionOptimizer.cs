using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class LerpFunctionOptimizer() : BaseMathFunctionOptimizer("Lerp", n => n is 3)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastLerpMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastLerpMethodDouble(context.FastMathFlags),
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

	private static string GenerateFastLerpMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast linear interpolation (Lerp) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a fused multiply-add formulation for numerical stability and performance. Returns a + t(b - a).</remarks>")
			.WriteLine("/// <param name=\"a\">Start value.</param>")
			.WriteLine("/// <param name=\"b\">End value.</param>")
			.WriteLine("/// <param name=\"t\">Interpolation factor.</param>")
			.WriteLine("/// <returns>The interpolated float value.</returns>")
			.WriteLine("private static float FastLerp(float a, float b, float t)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(a) || Single.IsNaN(b) || Single.IsNaN(t)) return Single.NaN;");
		}

		builder.WriteLine("return Single.FusedMultiplyAdd(t, b - a, a);")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastLerpMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast linear interpolation (Lerp) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a fused multiply-add formulation for numerical stability and performance. Returns a + t(b - a).</remarks>")
			.WriteLine("/// <param name=\"a\">Start value.</param>")
			.WriteLine("/// <param name=\"b\">End value.</param>")
			.WriteLine("/// <param name=\"t\">Interpolation factor.</param>")
			.WriteLine("/// <returns>The interpolated double value.</returns>")
			.WriteLine("private static double FastLerp(double a, double b, double t)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(a) || Double.IsNaN(b) || Double.IsNaN(t)) return Double.NaN;");
		}

		builder.WriteLine("return Double.FusedMultiplyAdd(t, b - a, a);")
			.EndBlock();

		return builder.ToString();
	}
}
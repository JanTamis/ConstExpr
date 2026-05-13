using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AsinhFunctionOptimizer() : BaseMathFunctionOptimizer("Asinh", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAsinhMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastAsinhMethodDouble(context.FastMathFlags),
			_ => null,
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

	private static string GenerateFastAsinhMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of inverse hyperbolic sine (Asinh) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a direct logarithmic identity with FusedMultiplyAdd and optional NaN checks.</remarks>")
			.WriteLine("/// <param name=\"x\">Any finite floating-point value.</param>")
			.WriteLine("/// <returns>Approximate inverse hyperbolic sine value.</returns>")
			.WriteLine("private static float FastAsinh(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;")
				.WriteWhitespace();
		}

		builder.WriteLine("var ax = Single.Abs(x);")
			.WriteLine("var r = Single.Log(ax + Single.Sqrt(Single.FusedMultiplyAdd(ax, ax, 1.0f)));")
			.WriteWhitespace()
			.WriteLine("return Single.CopySign(r, x);")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastAsinhMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of inverse hyperbolic sine (Asinh) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses a direct logarithmic identity with FusedMultiplyAdd and optional NaN checks.</remarks>")
			.WriteLine("/// <param name=\"x\">Any finite floating-point value.</param>")
			.WriteLine("/// <returns>Approximate inverse hyperbolic sine value.</returns>")
			.WriteLine("private static double FastAsinh(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;")
				.WriteWhitespace();
		}

		builder.WriteLine("var ax = Double.Abs(x);")
			.WriteLine("var r = Double.Log(ax + Double.Sqrt(Double.FusedMultiplyAdd(ax, ax, 1.0)));")
			.WriteWhitespace()
			.WriteLine("return Double.CopySign(r, x);")
			.EndBlock();

		return builder.ToString();
	}
}
using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AcoshFunctionOptimizer() : BaseMathFunctionOptimizer("Acosh", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastAcoshMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastAcoshMethodDouble(context.FastMathFlags),
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

	private static string GenerateFastAcoshMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of inverse hyperbolic cosine (Acosh) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses piecewise approximation with special handling for large values and values near 1.0. Supports optional NaN checks.</remarks>")
			.WriteLine("""/// <param name="x">Input value in the range [1.0, ∞).</param>""")
			.WriteLine("""/// <returns>Approximate inverse hyperbolic cosine value, ln(x + √(x² - 1)).</returns>""")
			.WriteLine("private static float FastAcosh(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("if (x < 1.0f) x = 1.0f;")
			.WriteWhitespace()
			.WriteLine("if (x > 1e7f)")
			.StartBlock()
			.WriteLine("return Single.Log(2.0f * x);")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("if (x < 1.5f)")
			.StartBlock()
			.WriteLine("var t = x - 1.0f;")
			.WriteLine("var sqrt2t = Single.Sqrt(2.0f * t);")
			.WriteLine("var correction = Single.FusedMultiplyAdd(t, Single.FusedMultiplyAdd(t, 0.01875f, -0.0833333f), 1.0f);")
			.WriteLine("return sqrt2t * correction;")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("var sqrtTerm = Single.Sqrt(Single.FusedMultiplyAdd(x, x, -1.0f));")
			.WriteLine("return Single.Log(x + sqrtTerm);")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastAcoshMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of inverse hyperbolic cosine (Acosh) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses piecewise approximation with higher precision coefficients. Special handling for very large values and values near 1.0. Supports optional NaN checks.</remarks>")
			.WriteLine("""/// <param name="x">Input value in the range [1.0, ∞).</param>""")
			.WriteLine("""/// <returns>Approximate inverse hyperbolic cosine value, ln(x + √(x² - 1)).</returns>""")
			.WriteLine("private static double FastAcosh(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("if (x < 1.0) x = 1.0;")
			.WriteWhitespace()
			.WriteLine("if (x > 1e15)")
			.StartBlock()
			.WriteLine("return Double.Log(2.0 * x);")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("if (x < 1.5)")
			.StartBlock()
			.WriteLine("var t = x - 1.0;")
			.WriteLine("var sqrt2t = Double.Sqrt(2.0 * t);")
			.WriteLine("var correction = Double.FusedMultiplyAdd(t, Double.FusedMultiplyAdd(t, Double.FusedMultiplyAdd(t, -0.005580357, 0.01875), -0.083333333333), 1.0);")
			.WriteLine("return sqrt2t * correction;")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("var sqrtTerm = Double.Sqrt(Double.FusedMultiplyAdd(x, x, -1.0));")
			.WriteLine("return Double.Log(x + sqrtTerm);")
			.EndBlock();

		return builder.ToString();
	}
}
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

	/// <summary>
	///   Generates a fast approximation implementation of the inverse hyperbolic cosine (Acosh) function for single-precision
	///   floating-point numbers.
	/// </summary>
	/// <param name="flags">FastMath flags that control NaN handling and other optimizations.</param>
	/// <returns>A string containing the C# code for the fast Acosh implementation.</returns>
	private static string GenerateFastAcoshMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of inverse hyperbolic cosine (Acosh) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses piecewise approximation with special handling for values near 1.0. Supports optional NaN checks.</remarks>")
			.WriteLine("""/// <param name="x">Input value in the range [1.0, ∞). Values above ~1.84e19 return +Infinity.</param>""")
			.WriteLine("""/// <returns>Approximate inverse hyperbolic cosine value, ln(x + √(x² - 1)).</returns>""")
			.WriteLine("private static float FastAcosh(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("x = Single.MaxNative(x, 1.0f);")
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
			.WriteLine("var arg  = x + sqrtTerm;")
			.WriteLine("var bits = BitConverter.SingleToInt32Bits(arg);")
			.WriteLine("var e    = (bits >> 23) - 127;")
			.WriteLine("var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);")
			.WriteLine("var lnm  = Single.FusedMultiplyAdd(-0.056570851f, m,  0.447178975f);")
			.WriteLine("lnm      = Single.FusedMultiplyAdd(lnm, m, -1.469956800f);")
			.WriteLine("lnm      = Single.FusedMultiplyAdd(lnm, m,  2.821202636f);")
			.WriteLine("lnm      = Single.FusedMultiplyAdd(lnm, m, -1.741793927f);")
			.WriteLine("return Single.FusedMultiplyAdd(e, 0.6931471806f, lnm);")
			.EndBlock();

		return builder.ToString();
	}

	/// <summary>
	///   Generates a fast approximation implementation of the inverse hyperbolic cosine (Acosh) function for double-precision
	///   floating-point numbers.
	/// </summary>
	/// <param name="flags">FastMath flags that control NaN handling and other optimizations.</param>
	/// <returns>A string containing the C# code for the fast Acosh implementation.</returns>
	private static string GenerateFastAcoshMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of inverse hyperbolic cosine (Acosh) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Uses piecewise approximation with higher precision coefficients and special handling for values near 1.0. Supports optional NaN checks.</remarks>")
			.WriteLine("""/// <param name="x">Input value in the range [1.0, ∞). Values above ~1.34e154 return +Infinity.</param>""")
			.WriteLine("""/// <returns>Approximate inverse hyperbolic cosine value, ln(x + √(x² - 1)).</returns>""")
			.WriteLine("private static double FastAcosh(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("x = Double.MaxNative(x, 1.0);")
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
			.WriteLine("var arg  = x + sqrtTerm;")
			.WriteLine("var bits = BitConverter.DoubleToInt64Bits(arg);")
			.WriteLine("var e    = (int)((bits >> 52) - 1023L);")
			.WriteLine("var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);")
			.WriteLine("var lnm  = Double.FusedMultiplyAdd(-0.056570851, m,  0.447178975);")
			.WriteLine("lnm      = Double.FusedMultiplyAdd(lnm, m, -1.469956800);")
			.WriteLine("lnm      = Double.FusedMultiplyAdd(lnm, m,  2.821202636);")
			.WriteLine("lnm      = Double.FusedMultiplyAdd(lnm, m, -1.741793927);")
			.WriteLine("return Double.FusedMultiplyAdd(e, 0.6931471805599453094172321214581766, lnm);")
			.EndBlock();

		return builder.ToString();
	}
}
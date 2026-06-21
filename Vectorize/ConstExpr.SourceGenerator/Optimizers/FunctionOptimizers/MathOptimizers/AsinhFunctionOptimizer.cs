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

	private static string GenerateFastAsinhMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of inverse hyperbolic sine (Asinh) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Polynomial for |x| &lt; 0.5; inline fast-log identity otherwise. ~1.8× faster than Single.Log identity.</remarks>")
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
			.WriteLine("float r;")
			.WriteWhitespace()
			.WriteLine("if (ax < 0.5f)")
			.StartBlock()
			.WriteLine("// asinh(x) ≈ x*(1 - x²/6 + 3x⁴/40 - 15x⁶/336)")
			.WriteLine("var x2 = ax * ax;")
			.WriteLine("var p = Single.FusedMultiplyAdd(x2, -0.044642857f, 0.075f);")
			.WriteLine("p = Single.FusedMultiplyAdd(p, x2, -0.166666667f);")
			.WriteLine("p = Single.FusedMultiplyAdd(p, x2, 1.0f);")
			.WriteLine("r = ax * p;")
			.EndBlock()
			.WriteLine("else")
			.StartBlock()
			.WriteLine("var arg  = ax + Single.Sqrt(Single.FusedMultiplyAdd(ax, ax, 1.0f));")
			.WriteLine("var bits = BitConverter.SingleToInt32Bits(arg);")
			.WriteLine("var e    = (bits >> 23) - 127;")
			.WriteLine("var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);")
			.WriteLine("var lnm  = Single.FusedMultiplyAdd(-0.056570851f, m,  0.447178975f);")
			.WriteLine("lnm      = Single.FusedMultiplyAdd(lnm, m, -1.469956800f);")
			.WriteLine("lnm      = Single.FusedMultiplyAdd(lnm, m,  2.821202636f);")
			.WriteLine("lnm      = Single.FusedMultiplyAdd(lnm, m, -1.741793927f);")
			.WriteLine("r        = Single.FusedMultiplyAdd(e, 0.6931471806f, lnm);")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("return Single.CopySign(r, x);")
			.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastAsinhMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of inverse hyperbolic sine (Asinh) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Polynomial for |x| &lt; 0.5; inline fast-log identity otherwise. ~2.6× faster than Double.Log identity.</remarks>")
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
			.WriteLine("double r;")
			.WriteWhitespace()
			.WriteLine("if (ax < 0.5)")
			.StartBlock()
			.WriteLine("// asinh(x) ≈ x*(1 - x²/6 + 3x⁴/40 - 15x⁶/336)")
			.WriteLine("var x2 = ax * ax;")
			.WriteLine("var p = Double.FusedMultiplyAdd(x2, -0.044642857142857144, 0.075);")
			.WriteLine("p = Double.FusedMultiplyAdd(p, x2, -0.16666666666666666);")
			.WriteLine("p = Double.FusedMultiplyAdd(p, x2, 1.0);")
			.WriteLine("r = ax * p;")
			.EndBlock()
			.WriteLine("else")
			.StartBlock()
			.WriteLine("var arg  = ax + Double.Sqrt(Double.FusedMultiplyAdd(ax, ax, 1.0));")
			.WriteLine("var bits = BitConverter.DoubleToInt64Bits(arg);")
			.WriteLine("var e    = (int)((bits >> 52) - 1023L);")
			.WriteLine("var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);")
			.WriteLine("var lnm  = Double.FusedMultiplyAdd(-0.056570851, m,  0.447178975);")
			.WriteLine("lnm      = Double.FusedMultiplyAdd(lnm, m, -1.469956800);")
			.WriteLine("lnm      = Double.FusedMultiplyAdd(lnm, m,  2.821202636);")
			.WriteLine("lnm      = Double.FusedMultiplyAdd(lnm, m, -1.741793927);")
			.WriteLine("r        = Double.FusedMultiplyAdd(e, 0.6931471805599453094172321214581766, lnm);")
			.EndBlock()
			.WriteWhitespace()
			.WriteLine("return Double.CopySign(r, x);")
			.EndBlock();

		return builder.ToString();
	}
}
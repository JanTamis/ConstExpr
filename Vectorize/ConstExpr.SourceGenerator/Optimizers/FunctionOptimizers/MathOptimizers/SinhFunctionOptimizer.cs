using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SinhFunctionOptimizer() : BaseMathFunctionOptimizer("Sinh", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastSinhMethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastSinhMethodDouble(context.FastMathFlags),
			_ => null
		});

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastSinhMethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of hyperbolic sine (Sinh) for single-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Inline fast-exp base-2 reduction with reciprocal-estimate refinement. ~1.1× faster than Single.Exp path.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate hyperbolic sine value.</returns>")
			.WriteLine("private static float FastSinh(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		builder.WriteLine("var sign = x;")
			.WriteLine("x = Single.Abs(x);")
			.WriteWhitespace()
			.WriteLine("if (x > 88.0f) return Single.CopySign(float.PositiveInfinity, sign);")
			.WriteWhitespace()
			.WriteLine("var kf = x * 1.4426950408889634f;")
			.WriteLine("var k  = (int)Single.Round(kf);")
			.WriteLine("var rf = kf - k;")
			.WriteLine("var p  = Single.FusedMultiplyAdd(0.055504108664821580f, rf, 0.240226506959100690f);")
			.WriteLine("p      = Single.FusedMultiplyAdd(p, rf, 0.693147180559945309f);")
			.WriteLine("var ex = Single.FusedMultiplyAdd(p, rf, 1.0f) * BitConverter.Int32BitsToSingle((k + 127) << 23);")
			.WriteWhitespace()
			.WriteLine("var r = Single.ReciprocalEstimate(ex);")
			.WriteLine("r *= Single.FusedMultiplyAdd(-ex, r, 2.0f);")
			.WriteWhitespace()
			.WriteLine("return Single.CopySign((ex - r) * 0.5f, sign);");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastSinhMethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("/// <summary>Fast approximation of hyperbolic sine (Sinh) for double-precision floating-point values.</summary>")
			.WriteLine("/// <remarks>Inline fast-exp base-2 reduction. ~1.5× faster than Double.Exp path.</remarks>")
			.WriteLine("/// <param name=\"x\">Input value.</param>")
			.WriteLine("/// <returns>Approximate hyperbolic sine value.</returns>")
			.WriteLine("private static double FastSinh(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		builder.WriteLine("var sign = x;")
			.WriteLine("x = Double.Abs(x);")
			.WriteWhitespace()
			.WriteLine("if (x > 709.0) return Double.CopySign(double.PositiveInfinity, sign);")
			.WriteWhitespace()
			.WriteLine("var kf = x * 1.4426950408889634073599246810018921;")
			.WriteLine("var k  = (long)Double.Round(kf);")
			.WriteLine("var rd = kf - k;")
			.WriteLine("var p  = Double.FusedMultiplyAdd(9.618129107628477232e-3, rd, 5.550410866482157995e-2);")
			.WriteLine("p      = Double.FusedMultiplyAdd(p, rd, 2.402265069591006909e-1);")
			.WriteLine("p      = Double.FusedMultiplyAdd(p, rd, 6.931471805599453094e-1);")
			.WriteLine("var ex = Double.FusedMultiplyAdd(p, rd, 1.0) * BitConverter.UInt64BitsToDouble((ulong)((k + 1023L) << 52));")
			.WriteWhitespace()
			.WriteLine("return Double.CopySign((ex - 1.0 / ex) * 0.5, sign);");

		builder.EndBlock();

		return builder.ToString();
	}
}
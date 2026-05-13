using System.Diagnostics.CodeAnalysis;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Exp10FunctionOptimizer() : BaseMathFunctionOptimizer("Exp10", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastExp10MethodFloat(context.FastMathFlags),
			SpecialType.System_Double => GenerateFastExp10MethodDouble(context.FastMathFlags),
			_ => null
		});

		if (method is not null)
		{
			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: keep as Exp10 call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastExp10MethodFloat(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static float FastExp10(float x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Single.IsNaN(x)) return Single.NaN;");
		}

		if (!flags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;")
				.WriteLine("if (Single.IsNegativeInfinity(x)) return 0.0f;");
		}

		builder.WriteLine("if (x == 0.0f) return 1.0f; // handles +0 and -0")
			.WriteWhitespace()
			.WriteLine("if (x >= 38.53f) return Single.PositiveInfinity;")
			.WriteLine("if (x <= -38.53f) return 0.0f;")
			.WriteWhitespace()
			// .WriteLine("// Reduce: k = round(x * log₂10), r = x − k * log₁₀2")
			// .WriteLine("// So 10^x = 2^k * 10^r,  r ∈ [−log₁₀2/2, log₁₀2/2] ≈ [−0.151, 0.151].")
			// .WriteLine("// Saves one MUL compared to first computing y = x * LN10 separately.")
			// .WriteLine("const float LOG2_10 = 3.321928094887362f;    // log₂(10)")
			// .WriteLine("const float LOG10_2 = 0.30102999566398120f;  // log₁₀(2) = 1/log₂(10)")
			// .WriteWhitespace()
			.WriteLine("var kf = x * 3.321928094887362f;")
			.WriteLine("var k  = (int)(kf + (kf >= 0.0f ? 0.5f : -0.5f));")
			.WriteLine("var r  = Single.FusedMultiplyAdd(-k, 0.30102999566398120f, x);")
			.WriteWhitespace()
			// .WriteLine("// Degree-4 Horner for 10^r: cₙ = ln(10)ⁿ / n!")
			// .WriteLine("// Max relative error ≈ 4e-5 (fast-math trade-off).")
			// .WriteLine("const float c4 = 1.1712551f;  // ln(10)⁴ / 24")
			// .WriteLine("const float c3 = 1.1712551f;  // ln(10)³ / 6")
			// .WriteLine("const float c2 = 2.6509491f;  // ln(10)² / 2")
			// .WriteLine("const float c1 = 2.3025851f;  // ln(10)")
			// .WriteWhitespace()
			.WriteLine("var poly = Single.FusedMultiplyAdd(1.1712551f, r, 1.1712551f);")
			.WriteLine("poly = Single.FusedMultiplyAdd(poly, r, 2.6509491f);")
			.WriteLine("poly = Single.FusedMultiplyAdd(poly, r, 2.3025851f);")
			.WriteLine("var expR = Single.FusedMultiplyAdd(poly, r, 1.0f);")
			.WriteWhitespace()
			.WriteLine("var bits = (k + 127) << 23;")
			.WriteLine("var scale = BitConverter.Int32BitsToSingle(bits);")
			.WriteLine("return scale * expR;");

		builder.EndBlock();

		return builder.ToString();
	}

	private static string GenerateFastExp10MethodDouble(FastMathFlags flags)
	{
		var builder = new CodeWriter();

		builder.WriteLine("private static double FastExp10(double x)")
			.StartBlock();

		if (!flags.HasFlag(FastMathFlags.NoNaN))
		{
			builder.WriteLine("if (Double.IsNaN(x)) return Double.NaN;");
		}

		if (!flags.HasFlag(FastMathFlags.NoInfinity))
		{
			builder.WriteLine("if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;")
				.WriteLine("if (Double.IsNegativeInfinity(x)) return 0.0;");
		}

		builder.WriteLine("if (x == 0.0) return 1.0; // handles +0 and -0")
			.WriteWhitespace()
			.WriteLine("if (x >= 309.0) return Double.PositiveInfinity;")
			.WriteLine("if (x <= -309.0) return 0.0;")
			.WriteWhitespace()
			// .WriteLine("// Reduce: k = round(x * log₂10), r = x − k * log₁₀2")
			// .WriteLine("// So 10^x = 2^k * 10^r,  r ∈ [−log₁₀2/2, log₁₀2/2] ≈ [−0.151, 0.151].")
			// .WriteLine("// Saves one MUL compared to first computing y = x * LN10 separately.")
			// .WriteLine("const double LOG2_10 = 3.321928094887362347870319429489390;")
			// .WriteLine("const double LOG10_2 = 0.30102999566398119521373889472449303;")
			// .WriteWhitespace()
			.WriteLine("var kf = x * 3.321928094887362347870319429489390;")
			.WriteLine("var k  = (long)(kf + (kf >= 0.0 ? 0.5 : -0.5));")
			.WriteLine("var r  = System.Math.FusedMultiplyAdd(-k, 0.30102999566398119521373889472449303, x);")
			.WriteWhitespace()
			// .WriteLine("// Degree-4 Horner for 10^r: cₙ = ln(10)ⁿ / n!")
			// .WriteLine("// Max relative error ≈ 4e-5 (fast-math trade-off).")
			// .WriteLine("const double c4 = 1.1712551489122673;  // ln(10)⁴ / 24")
			// .WriteLine("const double c3 = 2.0346785922934770;  // ln(10)³ / 6")
			// .WriteLine("const double c2 = 2.6509490552391997;  // ln(10)² / 2")
			// .WriteLine("const double c1 = 2.302585092994046;   // ln(10)")
			// .WriteWhitespace()
			.WriteLine("var poly = Double.FusedMultiplyAdd(1.1712551489122673, r, 2.0346785922934770);")
			.WriteLine("poly = Double.FusedMultiplyAdd(poly, r, 2.6509490552391997);")
			.WriteLine("poly = Double.FusedMultiplyAdd(poly, r, 2.302585092994046);")
			.WriteLine("var expR = Double.FusedMultiplyAdd(poly, r, 1.0);")
			.WriteWhitespace()
			.WriteLine("var bits = (ulong)((k + 1023L) << 52);")
			.WriteLine("var scale = BitConverter.UInt64BitsToDouble(bits);")
			.WriteLine("return scale * expR;");

		builder.EndBlock();

		return builder.ToString();
	}
}
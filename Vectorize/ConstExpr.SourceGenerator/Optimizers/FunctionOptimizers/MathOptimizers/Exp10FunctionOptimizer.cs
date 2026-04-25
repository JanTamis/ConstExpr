using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Exp10FunctionOptimizer() : BaseMathFunctionOptimizer("Exp10", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var method = ParseMethodFromString(paramType.SpecialType switch
		{
			SpecialType.System_Single => GenerateFastExp10MethodFloat(),
			SpecialType.System_Double => GenerateFastExp10MethodDouble(),
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

	private static string GenerateFastExp10MethodFloat()
	{
		return """
			private static float FastExp10(float x)
			{
				// Preserve special cases like MathF.Pow does
				if (Single.IsNaN(x)) return Single.NaN;
				if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;
				if (Single.IsNegativeInfinity(x)) return 0.0f;
				if (x == 0.0f) return 1.0f; // handles +0 and -0

				if (x >= 38.53f) return Single.PositiveInfinity;
				if (x <= -38.53f) return 0.0f;

				// Reduce: k = round(x * log₂10), r = x − k * log₁₀2
				// So 10^x = 2^k * 10^r,  r ∈ [−log₁₀2/2, log₁₀2/2] ≈ [−0.151, 0.151].
				// Saves one MUL compared to first computing y = x * LN10 separately.
				const float LOG2_10 = 3.321928094887362f;    // log₂(10)
				const float LOG10_2 = 0.30102999566398120f;  // log₁₀(2) = 1/log₂(10)

				var kf = x * LOG2_10;
				var k  = (int)(kf + (kf >= 0.0f ? 0.5f : -0.5f));
				var r  = Single.FusedMultiplyAdd(-k, LOG10_2, x);

				// Degree-4 Horner for 10^r: cₙ = ln(10)ⁿ / n!
				// Max relative error ≈ 4e-5 (fast-math trade-off).
				const float c4 = 1.1712551f;  // ln(10)⁴ / 24
				const float c3 = 2.0346786f;  // ln(10)³ / 6
				const float c2 = 2.6509491f;  // ln(10)² / 2
				const float c1 = 2.3025851f;  // ln(10)

				var poly = Single.FusedMultiplyAdd(c4, r, c3);
				poly = Single.FusedMultiplyAdd(poly, r, c2);
				poly = Single.FusedMultiplyAdd(poly, r, c1);
				var expR = Single.FusedMultiplyAdd(poly, r, 1.0f);

				var bits = (k + 127) << 23;
				var scale = BitConverter.Int32BitsToSingle(bits);
				return scale * expR;
			}
			""";
	}

	private static string GenerateFastExp10MethodDouble()
	{
		return """
			private static double FastExp10(double x)
			{
				// Preserve special cases like Math.Pow does
				if (Double.IsNaN(x)) return Double.NaN;
				if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;
				if (Double.IsNegativeInfinity(x)) return 0.0;
				if (x == 0.0) return 1.0; // handles +0 and -0

				if (x >= 309.0) return Double.PositiveInfinity;
				if (x <= -309.0) return 0.0;

				// Reduce: k = round(x * log₂10), r = x − k * log₁₀2
				// So 10^x = 2^k * 10^r,  r ∈ [−log₁₀2/2, log₁₀2/2] ≈ [−0.151, 0.151].
				// Saves one MUL compared to first computing y = x * LN10 separately.
				const double LOG2_10 = 3.321928094887362347870319429489390;
				const double LOG10_2 = 0.30102999566398119521373889472449303;

				var kf = x * LOG2_10;
				var k  = (long)(kf + (kf >= 0.0 ? 0.5 : -0.5));
				var r  = System.Math.FusedMultiplyAdd(-k, LOG10_2, x);

				// Degree-4 Horner for 10^r: cₙ = ln(10)ⁿ / n!
				// Max relative error ≈ 4e-5 (fast-math trade-off).
				const double c4 = 1.1712551489122673;  // ln(10)⁴ / 24
				const double c3 = 2.0346785922934770;  // ln(10)³ / 6
				const double c2 = 2.6509490552391997;  // ln(10)² / 2
				const double c1 = 2.302585092994046;   // ln(10)

				var poly = Double.FusedMultiplyAdd(c4, r, c3);
				poly = Double.FusedMultiplyAdd(poly, r, c2);
				poly = Double.FusedMultiplyAdd(poly, r, c1);
				var expR = Double.FusedMultiplyAdd(poly, r, 1.0);

				var bits = (ulong)((k + 1023L) << 52);
				var scale = BitConverter.UInt64BitsToDouble(bits);
				return scale * expR;
			}
			""";
	}
}
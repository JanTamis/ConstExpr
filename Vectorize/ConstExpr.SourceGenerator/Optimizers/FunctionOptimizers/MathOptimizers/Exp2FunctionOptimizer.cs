using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Exp2FunctionOptimizer() : BaseMathFunctionOptimizer("Exp2", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastExp2MethodFloat()
				: GenerateFastExp2MethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastExp2", context.VisitedParameters);
			return true;
		}

		// Default: keep as Exp2 call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastExp2MethodFloat()
	{
		return """
			private static float FastExp2(float x)
			{
				if (Single.IsNaN(x)) return Single.NaN;
				if (x >= 128.0f) return float.PositiveInfinity;
				if (x < -150.0f) return 0.0f;

				// Round to nearest integer; r = fractional part in [-0.5, 0.5]
				var k = (int)(x + (x >= 0.0f ? 0.5f : -0.5f));
				var r = x - k;

				// Evaluate 2^r directly with a degree-4 Horner polynomial.
				// Coefficients c_n = ln(2)^n / n! — no intermediate r*ln2 multiply needed.
				// Benchmark result (Apple M4 Pro, ARM64): 0.95 ns vs 1.35 ns for the previous
				// formulation (4 FMAs + 1 MUL); ~29 % faster.
				const float c4 = 0.009618129f;  // ln(2)^4 / 24
				const float c3 = 0.055504109f;  // ln(2)^3 / 6
				const float c2 = 0.240226507f;  // ln(2)^2 / 2
				const float c1 = 0.693147181f;  // ln(2)

				var p    = Single.FusedMultiplyAdd(c4, r, c3);
				p        = Single.FusedMultiplyAdd(p,  r, c2);
				p        = Single.FusedMultiplyAdd(p,  r, c1);
				var expR = Single.FusedMultiplyAdd(p,  r, 1.0f);

				var bits = (k + 127) << 23;
				var scale = BitConverter.Int32BitsToSingle(bits);
				return scale * expR;
			}
			""";
	}

	private static string GenerateFastExp2MethodDouble()
	{
		return """
			private static double FastExp2(double x)
			{
				if (Double.IsNaN(x)) return Double.NaN;
				if (x >= 1024.0) return Double.PositiveInfinity;
				if (x < -1100.0) return 0.0;

				var k = (long)(x + (x >= 0.0 ? 0.5 : -0.5));
				var r = x - k;

				// Evaluate 2^r directly with a degree-4 Horner polynomial.
				// Coefficients c_n = ln(2)^n / n! — no intermediate r*ln2 multiply needed.
				// Benchmark result (Apple M4 Pro, ARM64): 0.95 ns vs 1.32 ns for the previous
				// formulation (4 FMAs + 1 MUL); ~28 % faster.
				const double c4 = 9.618129107628477e-3;  // ln(2)^4 / 24
				const double c3 = 5.550410866482158e-2;  // ln(2)^3 / 6
				const double c2 = 2.402265069591007e-1;  // ln(2)^2 / 2
				const double c1 = 6.931471805599453e-1;  // ln(2)

				var p    = Double.FusedMultiplyAdd(c4, r, c3);
				p        = Double.FusedMultiplyAdd(p,  r, c2);
				p        = Double.FusedMultiplyAdd(p,  r, c1);
				var expR = Double.FusedMultiplyAdd(p,  r, 1.0);

				var bits = (ulong)((k + 1023L) << 52);
				var scale = BitConverter.UInt64BitsToDouble(bits);
				return scale * expR;
			}
			""";
	}
}
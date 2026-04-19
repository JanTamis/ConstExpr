using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Log10FunctionOptimizer() : BaseMathFunctionOptimizer("Log10", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Log10(Exp10(x)) => x  (inverse-operation cancellation)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Exp10" }, ArgumentList.Arguments.Count: 1 } inv
		    && IsPure(inv.ArgumentList.Arguments[0].Expression))
		{
			result = inv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		// For float / double: replace with a scalar fast polynomial approximation.
		// FastLog10V2 uses a degree-4 Horner polynomial whose coefficients are the
		// ln(m) minimax coefficients pre-multiplied by log10(e), eliminating the
		// final lnm * LOG10_E multiply.  Benchmark speedup vs Math.Log10:
		//   float  ≈ 2.0×  (1.782 ns → 0.897 ns, Apple M4 Pro / ARM64 RyuJIT)
		//   double ≈ 2.3×  (2.020 ns → 0.892 ns)
		// Max relative error ≈ 8.7e-5 (fast-math trade-off).
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastLog10MethodFloat()
				: GenerateFastLog10MethodDouble();

			context.AdditionalSyntax.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastLog10", context.VisitedParameters);
			return true;
		}

		// Default: delegate to the numeric-helper type's Log10.
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastLog10MethodFloat()
	{
		return """
			private static float FastLog10(float x)
			{
				if (Single.IsNaN(x) || x < 0f) return Single.NaN;
				if (x == 0f) return Single.NegativeInfinity;
				if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;

				// Bit-extract base-2 exponent e and mantissa m ∈ [1, 2).
				var bits = BitConverter.SingleToInt32Bits(x);
				var e    = (bits >> 23) - 127;
				var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);

				// Degree-4 Horner polynomial for log10(m), m ∈ [1, 2).
				// Coefficients d_i = c_i * log10(e) are the ln(m) minimax coefficients
				// pre-multiplied by log10(e) = 1/ln(10), saving one post-multiplication.
				// Max relative error ≈ 8.7e-5 (fast-math trade-off).
				const float d4 = -0.024568408f;  // c4 * log10(e)
				const float d3 =  0.194207361f;  // c3 * log10(e)
				const float d2 = -0.638394127f;  // c2 * log10(e)
				const float d1 =  1.225232737f;  // c1 * log10(e)
				const float d0 = -0.756451491f;  // c0 * log10(e)

				var log10m = Single.FusedMultiplyAdd(d4, m, d3);
				log10m     = Single.FusedMultiplyAdd(log10m, m, d2);
				log10m     = Single.FusedMultiplyAdd(log10m, m, d1);
				log10m     = Single.FusedMultiplyAdd(log10m, m, d0);

				const float LOG10_2 = 0.30102999566398120f;  // log10(2)
				return e * LOG10_2 + log10m;
			}
			""";
	}

	private static string GenerateFastLog10MethodDouble()
	{
		return """
			private static double FastLog10(double x)
			{
				if (Double.IsNaN(x) || x < 0.0) return Double.NaN;
				if (x == 0.0) return Double.NegativeInfinity;
				if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;

				// Bit-extract base-2 exponent e and mantissa m ∈ [1, 2).
				var bits = BitConverter.DoubleToInt64Bits(x);
				var e    = (int)((bits >> 52) - 1023L);
				var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);

				// Degree-4 Horner polynomial for log10(m), m ∈ [1, 2).
				// Coefficients d_i = c_i * log10(e) are the ln(m) minimax coefficients
				// pre-multiplied by log10(e) = 1/ln(10), saving one post-multiplication.
				// Max relative error ≈ 8.7e-5 (fast-math trade-off).
				const double d4 = -0.024568408426;  // c4 * log10(e)
				const double d3 =  0.194207361266;  // c3 * log10(e)
				const double d2 = -0.638394126876;  // c2 * log10(e)
				const double d1 =  1.225232737146;  // c1 * log10(e)
				const double d0 = -0.756451491109;  // c0 * log10(e)

				var log10m = Double.FusedMultiplyAdd(d4, m, d3);
				log10m     = Double.FusedMultiplyAdd(log10m, m, d2);
				log10m     = Double.FusedMultiplyAdd(log10m, m, d1);
				log10m     = Double.FusedMultiplyAdd(log10m, m, d0);

				const double LOG10_2 = 0.30102999566398119521373889472449303;  // log10(2)
				return e * LOG10_2 + log10m;
			}
			""";
	}
}
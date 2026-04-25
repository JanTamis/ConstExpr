using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Log2FunctionOptimizer() : BaseMathFunctionOptimizer("Log2", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Log2(Exp2(x)) => x  (inverse-operation cancellation)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Exp2" }, ArgumentList.Arguments.Count: 1 } inv
		    && IsPure(inv.ArgumentList.Arguments[0].Expression))
		{
			result = inv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		// For float / double: replace with a scalar fast polynomial approximation.
		// Uses a degree-4 Horner polynomial for log2(m), m ∈ [1, 2).
		// Coefficients d_i = c_i * log2(e) are the ln(m) minimax coefficients
		// pre-multiplied by log2(e) = 1/ln(2), so no post-division is needed.
		// log2(x) = e + log2(m)  where x = m * 2^e
		// Max relative error ≈ 8.7e-5 (fast-math trade-off).
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var method = ParseMethodFromString(paramType.SpecialType == SpecialType.System_Single
				? GenerateFastLog2MethodFloat()
				: GenerateFastLog2MethodDouble());

			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		// Default: delegate to the numeric-helper type's Log2.
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastLog2MethodFloat()
	{
		return """
			private static float FastLog2(float x)
			{
				if (Single.IsNaN(x) || x < 0f) return Single.NaN;
				if (x == 0f) return Single.NegativeInfinity;
				if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;

				// Bit-extract base-2 exponent e and mantissa m ∈ [1, 2).
				var bits = BitConverter.SingleToInt32Bits(x);
				var e    = (bits >> 23) - 127;
				var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);

				// Degree-4 Horner polynomial for log2(m), m ∈ [1, 2).
				// Coefficients d_i = c_i * log2(e) — the ln(m) minimax coefficients
				// pre-multiplied by log2(e) = 1.4426950408889634, eliminating the
				// final division by ln(2). Max relative error ≈ 8.7e-5 (fast-math).
				const float d4 = -0.081614484f;  // c4 * log2(e)
				const float d3 =  0.645142871f;  // c3 * log2(e)
				const float d2 = -2.120699326f;  // c2 * log2(e)
				const float d1 =  4.070134936f;  // c1 * log2(e)
				const float d0 = -2.512877389f;  // c0 * log2(e)

				var log2m = Single.FusedMultiplyAdd(d4, m, d3);
				log2m     = Single.FusedMultiplyAdd(log2m, m, d2);
				log2m     = Single.FusedMultiplyAdd(log2m, m, d1);
				log2m     = Single.FusedMultiplyAdd(log2m, m, d0);

				return e + log2m;
			}
			""";
	}

	private static string GenerateFastLog2MethodDouble()
	{
		return """
			private static double FastLog2(double x)
			{
				if (Double.IsNaN(x) || x < 0.0) return Double.NaN;
				if (x == 0.0) return Double.NegativeInfinity;
				if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;

				// Bit-extract base-2 exponent e and mantissa m ∈ [1, 2).
				var bits = BitConverter.DoubleToInt64Bits(x);
				var e    = (int)((bits >> 52) - 1023L);
				var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);

				// Degree-4 Horner polynomial for log2(m), m ∈ [1, 2).
				// Coefficients d_i = c_i * log2(e) — the ln(m) minimax coefficients
				// pre-multiplied by log2(e) = 1.4426950408889634, eliminating the
				// final division by ln(2). Max relative error ≈ 8.7e-5 (fast-math).
				const double d4 = -0.081614484028;  // c4 * log2(e)
				const double d3 =  0.645142871432;  // c3 * log2(e)
				const double d2 = -2.120699326246;  // c2 * log2(e)
				const double d1 =  4.070134936011;  // c1 * log2(e)
				const double d0 = -2.512877388986;  // c0 * log2(e)

				var log2m = Double.FusedMultiplyAdd(d4, m, d3);
				log2m     = Double.FusedMultiplyAdd(log2m, m, d2);
				log2m     = Double.FusedMultiplyAdd(log2m, m, d1);
				log2m     = Double.FusedMultiplyAdd(log2m, m, d0);

				return e + log2m;
			}
			""";
	}
}


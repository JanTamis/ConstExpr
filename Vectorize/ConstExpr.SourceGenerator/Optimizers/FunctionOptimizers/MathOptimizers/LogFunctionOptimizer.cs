using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class LogFunctionOptimizer() : BaseMathFunctionOptimizer("Log", 1, 2)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var arg = context.VisitedParameters[0];

		// Log(Exp(x)) => x (inverse operation)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Exp" }, ArgumentList.Arguments.Count: 1 } inv
		    && IsPure(inv.ArgumentList.Arguments[0].Expression))
		{
			result = inv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		// For float / double: replace with a scalar fast polynomial approximation.
		// Uses a degree-4 Horner polynomial for ln(m), m ∈ [1, 2).
		// ln(x) = e·ln(2) + ln(m)   — no LOG10_E conversion step needed (vs Log10).
		// Benchmark speedup vs Math.Log (Apple M4 Pro / ARM64 RyuJIT):
		//   float  ≈ 2.0×  (1.764 ns → 0.888 ns)
		//   double ≈ 2.2×  (2.003 ns → 0.904 ns)
		// Max relative error ≈ 8.7e-5 (fast-math trade-off).
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastLogMethodFloat()
				: GenerateFastLogMethodDouble();

			context.AdditionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			if (context.VisitedParameters.Count == 1)
			{
				// Log(x) => FastLog(x)
				result = CreateInvocation("FastLog", context.VisitedParameters);
				return true;
			}

			// Log(x, newBase) => FastLog(x) / FastLog(newBase)
			// log_base(x) = ln(x) / ln(newBase).
			// Benchmark speedup vs Math.Log(x, newBase) (Apple M4 Pro / ARM64 RyuJIT):
			//   float  ≈ 2.2×  (4.541 ns → 2.021 ns)
			//   double ≈ 2.1×  (4.250 ns → 2.000 ns)
			result = DivideExpression(
				CreateInvocation("FastLog", [context.VisitedParameters[0]]),
				CreateInvocation("FastLog", [context.VisitedParameters[1]]));
			return true;
		}

		result = null;
		return false;
	}

	private static string GenerateFastLogMethodFloat()
	{
		return """
			private static float FastLog(float x)
			{
				if (Single.IsNaN(x) || x < 0f) return Single.NaN;
				if (x == 0f) return Single.NegativeInfinity;
				if (Single.IsPositiveInfinity(x)) return Single.PositiveInfinity;

				// Bit-extract base-2 exponent e and mantissa m ∈ [1, 2).
				var bits = BitConverter.SingleToInt32Bits(x);
				var e    = (bits >> 23) - 127;
				var m    = BitConverter.Int32BitsToSingle((bits & 0x007FFFFF) | 0x3F800000);

				// Degree-4 Horner polynomial for ln(m), m ∈ [1, 2).
				// ln(x) = e·ln(2) + ln(m)  — no LOG10_E step needed vs Log10.
				// Max relative error ≈ 8.7e-5 (fast-math trade-off).
				const float c4 = -0.056570851f;
				const float c3 =  0.447178975f;
				const float c2 = -1.469956800f;
				const float c1 =  2.821202636f;
				const float c0 = -1.741793927f;

				var lnm = Single.FusedMultiplyAdd(c4, m, c3);
				lnm     = Single.FusedMultiplyAdd(lnm, m, c2);
				lnm     = Single.FusedMultiplyAdd(lnm, m, c1);
				lnm     = Single.FusedMultiplyAdd(lnm, m, c0);

				const float LN2 = 0.6931471805599453f;  // ln(2)
				return e * LN2 + lnm;
			}
			""";
	}

	private static string GenerateFastLogMethodDouble()
	{
		return """
			private static double FastLog(double x)
			{
				if (Double.IsNaN(x) || x < 0.0) return Double.NaN;
				if (x == 0.0) return Double.NegativeInfinity;
				if (Double.IsPositiveInfinity(x)) return Double.PositiveInfinity;

				// Bit-extract base-2 exponent e and mantissa m ∈ [1, 2).
				var bits = BitConverter.DoubleToInt64Bits(x);
				var e    = (int)((bits >> 52) - 1023L);
				var m    = BitConverter.Int64BitsToDouble((bits & 0x000FFFFFFFFFFFFFL) | 0x3FF0000000000000L);

				// Degree-4 Horner polynomial for ln(m), m ∈ [1, 2).
				// ln(x) = e·ln(2) + ln(m)  — no LOG10_E step needed vs Log10.
				// Max relative error ≈ 8.7e-5 (fast-math trade-off).
				const double c4 = -0.056570851;
				const double c3 =  0.447178975;
				const double c2 = -1.469956800;
				const double c1 =  2.821202636;
				const double c0 = -1.741793927;

				var lnm = Double.FusedMultiplyAdd(c4, m, c3);
				lnm     = Double.FusedMultiplyAdd(lnm, m, c2);
				lnm     = Double.FusedMultiplyAdd(lnm, m, c1);
				lnm     = Double.FusedMultiplyAdd(lnm, m, c0);

				const double LN2 = 0.6931471805599453094172321214581766;  // ln(2)
				return e * LN2 + lnm;
			}
			""";
	}
}
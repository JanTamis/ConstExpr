using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AcoshFunctionOptimizer() : BaseMathFunctionOptimizer("Acosh", n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var method = ParseMethodFromString(paramType.SpecialType == SpecialType.System_Single
				? GenerateFastAcoshMethodFloat()
				: GenerateFastAcoshMethodDouble());

			context.AdditionalSyntax.TryAdd(method, false);

			result = CreateInvocation(method.Identifier.Text, context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastAcoshMethodFloat()
	{
		return """
			private static float FastAcosh(float x)
			{
				if (Single.IsNaN(x)) return Single.NaN;
				if (x < 1.0f) x = 1.0f;
				
				if (x > 1e7f)
				{
					return Single.Log(2.0f * x);
				}
				
			// For values close to 1, use polynomial approximation with FMA to avoid log.
			// Taylor series: acosh(1+t)/sqrt(2t) = 1 − t/12 + 3t²/160 − …
			// Horner form:   1 + t*(−1/12 + t*(3/160)) = FMA(t, FMA(t, 3/160, −1/12), 1)
			if (x < 1.5f)
			{
				float t = x - 1.0f;
				float sqrt2t = Single.Sqrt(2.0f * t);
				float correction = Single.FusedMultiplyAdd(t, Single.FusedMultiplyAdd(t, 0.01875f, -0.0833333f), 1.0f);
				return sqrt2t * correction;
			}
				
				// Use FMA: sqrt(x^2 - 1)
				float sqrtTerm = Single.Sqrt(Single.FusedMultiplyAdd(x, x, -1.0f));
				return Single.Log(x + sqrtTerm);
			}
			""";
	}

	private static string GenerateFastAcoshMethodDouble()
	{
		return """
			private static double FastAcosh(double x)
			{
				if (Double.IsNaN(x)) return Double.NaN;
				if (x < 1.0) x = 1.0;
				
				if (x > 1e15)
				{
					return Double.Log(2.0 * x);
				}
				
			// For values close to 1, use polynomial approximation with FMA to avoid log.
			// Taylor series: acosh(1+t)/sqrt(2t) = 1 − t/12 + 3t²/160 − 5t³/896 − …
			// Horner form: FMA(t, FMA(t, FMA(t, −5/896, 3/160), −1/12), 1.0)
			if (x < 1.5)
			{
				double t = x - 1.0;
				double sqrt2t = Double.Sqrt(2.0 * t);
				double correction = Double.FusedMultiplyAdd(t, Double.FusedMultiplyAdd(t, Double.FusedMultiplyAdd(t, -0.005580357, 0.01875), -0.083333333333), 1.0);
				return sqrt2t * correction;
			}
				
				// Use FMA: sqrt(x^2 - 1)
				double sqrtTerm = Double.Sqrt(Double.FusedMultiplyAdd(x, x, -1.0));
				return Double.Log(x + sqrtTerm);
			}
			""";
	}
}
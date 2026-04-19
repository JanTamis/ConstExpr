using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AcosPiFunctionOptimizer() : BaseMathFunctionOptimizer("AcosPi", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastAcosPiMethodFloat()
				: GenerateFastAcosPiMethodDouble();

			context.AdditionalSyntax.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastAcosPi", context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastAcosPiMethodFloat()
	{
		return """
			private static float FastAcosPi(float x)
			{
				if (Single.IsNaN(x)) return Single.NaN;
				var negative = x < 0f;
				x = Single.Abs(x);
				if (x > 1.0f) x = 1.0f;
				
				// A&S §4.4.45 minimax polynomial (degree-3) coefficients pre-divided by π.
				// 3 FMAs + 1 sqrt. Max absolute error ≈ 5.4e-6 (vs 2.2e-4 for degree-2 §4.4.44).
				var p = Single.FusedMultiplyAdd(-0.00596227f, x, 0.02363378f);  // -0.0187293 / π, 0.0742610 / π
				p = Single.FusedMultiplyAdd(p, x, -0.06751894f);                // -0.2121144 / π
				p = Single.FusedMultiplyAdd(p, x, 0.5f);                        // π/2 / π = 0.5
				p *= Single.Sqrt(1f - x);
				
				// acosPi(-x) = 1 - acosPi(x)
				return negative ? 1f - p : p;
			}
			""";
	}

	private static string GenerateFastAcosPiMethodDouble()
	{
		return """
			private static double FastAcosPi(double x)
			{
				if (Double.IsNaN(x)) return Double.NaN;
				var negative = x < 0.0;
				x = Double.Abs(x);
				if (x > 1.0) x = 1.0;
				
				// A&S §4.4.45 minimax polynomial (degree-3) coefficients pre-divided by π.
				// Max absolute error ≈ 1.3e-6 (in units of π).
				var p = Double.FusedMultiplyAdd(-0.0059622704862860465, x, 0.023633778501171472);  // -0.0187293 / π, 0.0742610 / π
				p = Double.FusedMultiplyAdd(p, x, -0.067518943563376579);  // -0.2121144 / π
				p = Double.FusedMultiplyAdd(p, x, 0.5);                    // π/2 / π = 0.5
				p *= Double.Sqrt(1.0 - x);
				
				// acosPi(-x) = 1 - acosPi(x)
				return negative ? 1.0 - p : p;
			}
			""";
	}
}

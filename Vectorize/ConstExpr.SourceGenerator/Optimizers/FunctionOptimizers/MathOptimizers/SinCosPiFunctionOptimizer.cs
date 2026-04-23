using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SinCosPiFunctionOptimizer() : BaseMathFunctionOptimizer("SinCosPi",n => n is 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastSinCosPiMethodFloat()
				: GenerateFastSinCosPiMethodDouble();

			context.AdditionalSyntax.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastSinCosPi", context.VisitedParameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}

	private static string GenerateFastSinCosPiMethodFloat()
	{
		return """
			private static (float Sin, float Cos) FastSinCosPi(float x)
			{
				// Fast simultaneous sine(π*x) and cosine(π*x) calculation
				// Uses optimized polynomial approximation with branchless operations
				if (Single.IsNaN(x)) return (Single.NaN, Single.NaN);
				
				// Range reduction: bring x to [-1, 1]
				x -= Single.Round(x * 0.5f) * 2.0f;
				
				// Store original sign for sine and work with absolute value
				var originalSign = x;
				var absX = Single.Abs(x);
				
				// Determine if we're in upper or lower half [0, 0.5] vs (0.5, 1]
				// For upper half: sin(π*x) = sin(π*(1-x)), cos(π*x) = -cos(π*(1-x))
				var inUpperHalf = absX > 0.5f;
				var u = inUpperHalf ? (1.0f - absX) : absX;
				
				// u² shared for both polynomials — π absorbed into coefficients (saves 1 FMUL vs px = u*π)
				var u2 = u * u;
				
				// sinpi(u) = u·(π + u²·(−π³/6 + u²·(π⁵/120 + u²·(−π⁷/5040))))
				var sinVal = -0.5992645f;                                 // −π⁷/5040
				sinVal = Single.FusedMultiplyAdd(sinVal, u2,  2.5501640f); //  π⁵/120
				sinVal = Single.FusedMultiplyAdd(sinVal, u2, -5.1677128f); // −π³/6
				sinVal = Single.FusedMultiplyAdd(sinVal, u2,  3.1415927f); //  π
				sinVal = sinVal * u;
				
				// Apply original sign using CopySign (branchless)
				sinVal = Single.CopySign(sinVal, originalSign);
				
				// cospi(u) = 1 + u²·(−π²/2 + u²·(π⁴/24 + u²·(−π⁶/720)))
				var cosVal = -1.3352627f;                                  // −π⁶/720
				cosVal = Single.FusedMultiplyAdd(cosVal, u2,  4.0587121f); //  π⁴/24
				cosVal = Single.FusedMultiplyAdd(cosVal, u2, -4.9348022f); // −π²/2
				cosVal = Single.FusedMultiplyAdd(cosVal, u2,  1.0f);
				
				// For upper half, negate cosine
				cosVal = inUpperHalf ? -cosVal : cosVal;
				
				return (sinVal, cosVal);
			} 
			""";
	}

	private static string GenerateFastSinCosPiMethodDouble()
	{
		return """
			private static (double Sin, double Cos) FastSinCosPi(double x)
			{
				// Fast simultaneous sine(π*x) and cosine(π*x) calculation
				// Uses optimized polynomial approximation with branchless operations
				if (Double.IsNaN(x)) return (Double.NaN, Double.NaN);
				
				// Range reduction: bring x to [-1, 1]
				x -= Double.Round(x * 0.5) * 2.0;
				
				// Store original sign for sine and work with absolute value
				var originalSign = x;
				var absX = Double.Abs(x);
				
				// Determine if we're in upper or lower half [0, 0.5] vs (0.5, 1]
				// For upper half: sin(π*x) = sin(π*(1-x)), cos(π*x) = -cos(π*(1-x))
				var inUpperHalf = absX > 0.5;
				var u = inUpperHalf ? (1.0 - absX) : absX;
				
				// u² shared for both polynomials — π absorbed into coefficients (saves 1 FMUL vs px = u*π)
				var u2 = u * u;
				
				// sinpi(u) = u·(π + u²·(−π³/6 + u²·(π⁵/120 + u²·(−π⁷/5040 + u²·(π⁹/362880)))))
				var sinVal = 0.08214588661112823;                                    //  π⁹/362880
				sinVal = Double.FusedMultiplyAdd(sinVal, u2, -0.5992645293218801);   // −π⁷/5040
				sinVal = Double.FusedMultiplyAdd(sinVal, u2,  2.5501640398773455);   //  π⁵/120
				sinVal = Double.FusedMultiplyAdd(sinVal, u2, -5.1677127800499706);   // −π³/6
				sinVal = Double.FusedMultiplyAdd(sinVal, u2,  3.1415926535897932);   //  π
				sinVal = sinVal * u;
				
				// Apply original sign using CopySign (branchless)
				sinVal = Double.CopySign(sinVal, originalSign);
				
				// cospi(u) = 1 + u²·(−π²/2 + u²·(π⁴/24 + u²·(−π⁶/720 + u²·(π⁸/40320))))
				var cosVal = 0.23533075157732439;                                    //  π⁸/40320
				cosVal = Double.FusedMultiplyAdd(cosVal, u2, -1.3352627312227247);   // −π⁶/720
				cosVal = Double.FusedMultiplyAdd(cosVal, u2,  4.0587121264167682);   //  π⁴/24
				cosVal = Double.FusedMultiplyAdd(cosVal, u2, -4.9348022005446793);   // −π²/2
				cosVal = Double.FusedMultiplyAdd(cosVal, u2,  1.0);
				
				// For upper half, negate cosine
				cosVal = inUpperHalf ? -cosVal : cosVal;
				
				return (sinVal, cosVal);
			}
			""";
	}
}

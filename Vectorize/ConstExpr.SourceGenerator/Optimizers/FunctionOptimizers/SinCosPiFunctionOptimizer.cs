using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class SinCosPiFunctionOptimizer() : BaseFunctionOptimizer("SinCosPi", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		// When FastMath is enabled, add a fast sincospi approximation method
		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastSinCosPiMethodFloat()
				: GenerateFastSinCosPiMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastSinCosPi", parameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateFastSinCosPiMethodFloat()
	{
		return """
			private static (float Sin, float Cos) FastSinCosPi(float x)
			{
				// Fast simultaneous sine(π*x) and cosine(π*x) calculation
				// Uses optimized polynomial approximation with branchless operations
				
				// Range reduction: bring x to [-1, 1]
				x = x - Single.Round(x / 2.0f) * 2.0f;
				
				// Store original sign for sine and work with absolute value
				var originalSign = x;
				var absX = Single.Abs(x);
				
				// Determine if we're in upper or lower half [0, 0.5] vs (0.5, 1]
				var inUpperHalf = absX > 0.5f;
				
				// For upper half, use symmetry: sin(π*x) = sin(π*(1-x)), cos(π*x) = -cos(π*(1-x))
				var xReduced = inUpperHalf ? (1.0f - absX) : absX;
				
				// Compute π*x and its square
				var px = xReduced * Single.Pi;
				var px2 = px * px;
				
				// Sin: Taylor series with minimax coefficients
				var sinVal = -0.00019840874f;
				sinVal = Single.FusedMultiplyAdd(sinVal, px2, 0.0083333310f);
				sinVal = Single.FusedMultiplyAdd(sinVal, px2, -0.16666667f);
				sinVal = Single.FusedMultiplyAdd(sinVal, px2, 1.0f);
				sinVal = sinVal * px;
				
				// Apply original sign using CopySign (branchless)
				sinVal = Single.CopySign(sinVal, originalSign);
				
				// Cos: Taylor series with minimax coefficients
				// cos(x) ≈ 1 + x²*(-0.5 + x²*(0.041666667 + x²*(-0.0013888889)))
				var cosVal = -0.0013888397f;
				cosVal = Single.FusedMultiplyAdd(cosVal, px2, 0.041666418f);
				cosVal = Single.FusedMultiplyAdd(cosVal, px2, -0.5f);
				cosVal = Single.FusedMultiplyAdd(cosVal, px2, 1.0f);
				
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
				
				// Range reduction: bring x to [-1, 1]
				x = x - Double.Round(x / 2.0) * 2.0;
				
				// Store original sign for sine and work with absolute value
				var originalSign = x;
				var absX = Double.Abs(x);
				
				// Determine if we're in upper or lower half [0, 0.5] vs (0.5, 1]
				var inUpperHalf = absX > 0.5;
				
				// For upper half, use symmetry: sin(π*x) = sin(π*(1-x)), cos(π*x) = -cos(π*(1-x))
				var xReduced = inUpperHalf ? (1.0 - absX) : absX;
				
				// Compute π*x and its square
				var px = xReduced * Double.Pi;
				var px2 = px * px;
				
				// Sin: Taylor series with higher precision minimax coefficients
				var sinVal = 2.7557313707070068e-6;
				sinVal = Double.FusedMultiplyAdd(sinVal, px2, -0.00019841269841201856);
				sinVal = Double.FusedMultiplyAdd(sinVal, px2, 0.0083333333333331650);
				sinVal = Double.FusedMultiplyAdd(sinVal, px2, -0.16666666666666666);
				sinVal = Double.FusedMultiplyAdd(sinVal, px2, 1.0);
				sinVal = sinVal * px;
				
				// Apply original sign using CopySign (branchless)
				sinVal = Double.CopySign(sinVal, originalSign);
				
				// Cos: Taylor series with higher precision minimax coefficients
				// cos(x) ≈ 1 + x²*(-0.5 + x²*(0.041666667 + x²*(-0.000013888889 + x²*(0.0000002480159))))
				var cosVal = 2.6051615464872668e-5;
				cosVal = Double.FusedMultiplyAdd(cosVal, px2, -0.0013888888888887398);
				cosVal = Double.FusedMultiplyAdd(cosVal, px2, 0.041666666666666664);
				cosVal = Double.FusedMultiplyAdd(cosVal, px2, -0.5);
				cosVal = Double.FusedMultiplyAdd(cosVal, px2, 1.0);
				
				// For upper half, negate cosine
				cosVal = inUpperHalf ? -cosVal : cosVal;
				
				return (sinVal, cosVal);
			}
			""";
	}
}

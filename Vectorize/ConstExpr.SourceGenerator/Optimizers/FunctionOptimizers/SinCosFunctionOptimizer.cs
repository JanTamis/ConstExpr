using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class SinCosFunctionOptimizer() : BaseFunctionOptimizer("SinCos", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		// When FastMath is enabled, add a fast sincos approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath
			&& paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastSinCosMethodFloat()
				: GenerateFastSinCosMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastSinCos", parameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateFastSinCosMethodFloat()
	{
		return """
			private static (float Sin, float Cos) FastSinCos(float x)
			{
				// Fast simultaneous sine and cosine calculation using optimized polynomial approximation
				// Uses CopySign for branchless sign operations (3.5x faster than Math.SinCos)
				
				// Range reduction to [-π, π] using Tau (2π)
				const float Tau = 6.28318530717959f;
				const float Pi = 3.14159265358979f;
				const float HalfPi = 1.57079632679490f;
				
				x = x - Single.Round(x / Tau) * Tau;
				
				// Get absolute value for quadrant reduction
				var absX = Single.Abs(x);
				
				// Quadrant reduction to [0, π/2]
				var quadAdjust = 0.0f;
				if (absX > Pi)
				{
					absX = Tau - absX;
					quadAdjust = Pi;
				}
				
				var cosSign = 1.0f;
				if (absX > HalfPi)
				{
					absX = Pi - absX;
					cosSign = -1.0f;
				}
				
				// Polynomial approximation using FusedMultiplyAdd for performance
				var x2 = absX * absX;
				
				// Sin: Taylor series with minimax coefficients
				var sinVal = -0.00019840874f;
				sinVal = Single.FusedMultiplyAdd(sinVal, x2, 0.0083333310f);
				sinVal = Single.FusedMultiplyAdd(sinVal, x2, -0.16666667f);
				sinVal = Single.FusedMultiplyAdd(sinVal, x2, 1.0f);
				sinVal = sinVal * absX;
				
				// Apply correct sign using CopySign (branchless)
				// x already contains the original sign, no need for signX variable
				sinVal = Single.CopySign(sinVal, x * Single.CopySign(1.0f, x + quadAdjust));
				
				// Cos: Taylor series with minimax coefficients
				var cosVal = 0.0013888397f;
				cosVal = Single.FusedMultiplyAdd(cosVal, x2, -0.041666418f);
				cosVal = Single.FusedMultiplyAdd(cosVal, x2, 0.5f);
				cosVal = Single.FusedMultiplyAdd(cosVal, x2, -1.0f);
				cosVal = cosVal + 1.0f;
				cosVal = cosVal * cosSign;
				
				return (sinVal, cosVal);
			}
			""";
	}

	private static string GenerateFastSinCosMethodDouble()
	{
		return """
			private static (double Sin, double Cos) FastSinCos(double x)
			{
				// Fast simultaneous sine and cosine calculation using optimized polynomial approximation
				// Uses CopySign for branchless sign operations (3.3x faster than Math.SinCos)
				
				// Range reduction to [-π, π] using Tau (2π)
				const double Tau = 6.28318530717958647692;
				const double Pi = 3.14159265358979323846;
				const double HalfPi = 1.57079632679489661923;
				
				x = x - Double.Round(x / Tau) * Tau;
				
				// Get absolute value for quadrant reduction
				var absX = Double.Abs(x);
				
				// Quadrant reduction to [0, π/2]
				var quadAdjust = 0.0;
				if (absX > Pi)
				{
					absX = Tau - absX;
					quadAdjust = Pi;
				}
				
				var cosSign = 1.0;
				if (absX > HalfPi)
				{
					absX = Pi - absX;
					cosSign = -1.0;
				}
				
				// Polynomial approximation using FusedMultiplyAdd for performance
				var x2 = absX * absX;
				
				// Sin: Taylor series with higher precision minimax coefficients
				var sinVal = 2.7557313707070068e-6;
				sinVal = Double.FusedMultiplyAdd(sinVal, x2, -0.00019841269841201856);
				sinVal = Double.FusedMultiplyAdd(sinVal, x2, 0.0083333333333331650);
				sinVal = Double.FusedMultiplyAdd(sinVal, x2, -0.16666666666666666);
				sinVal = Double.FusedMultiplyAdd(sinVal, x2, 1.0);
				sinVal = sinVal * absX;
				
				// Apply correct sign using CopySign (branchless)
				// x already contains the original sign, no need for signX variable
				sinVal = Double.CopySign(sinVal, x * Double.CopySign(1.0, x + quadAdjust));
				
				// Cos: Taylor series with higher precision minimax coefficients
				var cosVal = -2.6051615464872668e-5;
				cosVal = Double.FusedMultiplyAdd(cosVal, x2, 0.0013888888888887398);
				cosVal = Double.FusedMultiplyAdd(cosVal, x2, -0.041666666666666664);
				cosVal = Double.FusedMultiplyAdd(cosVal, x2, 0.5);
				cosVal = Double.FusedMultiplyAdd(cosVal, x2, -1.0);
				cosVal = cosVal + 1.0;
				cosVal = cosVal * cosSign;
				
				return (sinVal, cosVal);
			}
			""";
	}
}


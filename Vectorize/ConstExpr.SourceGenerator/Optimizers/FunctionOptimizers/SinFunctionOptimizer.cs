using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class SinFunctionOptimizer() : BaseFunctionOptimizer("Sin", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastSinMethodFloat()
				: GenerateFastSinMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastSin", parameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateFastSinMethodFloat()
	{
		return """
			private static float FastSin(float x)
			{
				// Fast sine approximation using optimized minimax polynomial
				// Uses range reduction and symmetry properties of sine
				
				const float TwoPi = 6.28318530717958647692f;
				const float Pi = 3.14159265358979323846f;
				const float HalfPi = 1.57079632679489661923f;
				
				// Store original sign for CopySign
				var originalX = x;
				
				// Range reduction: bring x to [-π, π]
				x = x - Single.Floor(x / TwoPi) * TwoPi;
				if (x > Pi) x -= TwoPi;
				if (x < -Pi) x += TwoPi;
				
				// Use absolute value
				x = Single.Abs(x);
				
				// Use symmetry: sin(x) for x > π/2 is sin(π - x)
				if (x > HalfPi)
				{
					x = Pi - x;
				}
				
				// Taylor series approximation with optimized coefficients
				// sin(x) ≈ x - x³/3! + x⁵/5! - x⁷/7! + x⁹/9!
				var x2 = x * x;
				var ret = 2.6019406621361745e-6f;
				ret = Single.FusedMultiplyAdd(ret, x2, -0.00019839531932f);
				ret = Single.FusedMultiplyAdd(ret, x2, 0.0083333333333f);
				ret = Single.FusedMultiplyAdd(ret, x2, -0.16666666666f);
				ret = Single.FusedMultiplyAdd(ret, x2, 1.0f);
				ret *= x;
				
				// Apply original sign using CopySign (faster than manual sign handling)
				return Single.CopySign(ret, originalX);
			}
			""";
	}

	private static string GenerateFastSinMethodDouble()
	{
		return """
			private static double FastSin(double x)
			{
				// Fast sine approximation for double precision
				// Uses range reduction and symmetry properties of sine
				
				const double TwoPi = 6.283185307179586476925286766559;
				const double Pi = 3.1415926535897932384626433832795;
				const double HalfPi = 1.5707963267948966192313216916398;
				
				// Store original sign for CopySign
				var originalX = x;
				
				// Range reduction: bring x to [-π, π]
				x = x - Double.Floor(x / TwoPi) * TwoPi;
				if (x > Pi) x -= TwoPi;
				if (x < -Pi) x += TwoPi;
				
				// Use absolute value
				x = Double.Abs(x);
				
				// Use symmetry: sin(x) for x > π/2 is sin(π - x)
				if (x > HalfPi)
				{
					x = Pi - x;
				}
				
				// Taylor series approximation with optimized coefficients
				// Higher precision for double
				var x2 = x * x;
				var ret = 2.6019406621361745e-9;
				ret = Double.FusedMultiplyAdd(ret, x2, -1.9839531932589676e-7);
				ret = Double.FusedMultiplyAdd(ret, x2, 8.3333333333216515e-6);
				ret = Double.FusedMultiplyAdd(ret, x2, -0.00019841269836761127);
				ret = Double.FusedMultiplyAdd(ret, x2, 0.0083333333333332177);
				ret = Double.FusedMultiplyAdd(ret, x2, -0.16666666666666666);
				ret = Double.FusedMultiplyAdd(ret, x2, 1.0);
				ret *= x;
				
				// Apply original sign using CopySign (faster than manual sign handling)
				return Double.CopySign(ret, originalX);
			}
			""";
	}
}

using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AsinPiFunctionOptimizer() : BaseMathFunctionOptimizer("AsinPi", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(method, out var paramType))
		{
			return false;
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastAsinPiMethodFloat()
				: GenerateFastAsinPiMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastAsinPi", parameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateFastAsinPiMethodFloat()
	{
		return """
			private static float FastAsinPi(float x)
			{
				// Clamp input to valid range [-1, 1]
				if (x < -1.0f) x = -1.0f;
				if (x > 1.0f) x = 1.0f;
				
				// For AsinPi, we compute asin(x)/π
				// Use a better polynomial approximation based on sqrt(1-x) for better accuracy
				var xa = Single.Abs(x);
				
				// For small values, use Taylor series: asin(x)/π ≈ x/π + x³/(6π) + ...
				// For larger values, use sqrt-based approximation
				if (xa < 0.5f)
				{
					// Taylor series approach for small values
					var x2 = xa * xa;
					var ret = 0.16666667f;  // 1/6
					ret = Single.FusedMultiplyAdd(ret, x2, 1.0f);
					ret = ret * xa * 0.31830988618379067f;  // 1/π
					return Single.CopySign(ret, x);
				}
				else
				{
					// sqrt-based approximation for larger values
					// asin(x) ≈ π/2 - sqrt(1-x) * (c0 + c1*x + c2*x²)
					var onemx = 1.0f - xa;
					var sqrt_onemx = Single.Sqrt(onemx);
					
					var ret = -0.0187293f;
					ret = Single.FusedMultiplyAdd(ret, xa, 0.0742610f);
					ret = Single.FusedMultiplyAdd(ret, xa, -0.2121144f);
					ret = Single.FusedMultiplyAdd(ret, xa, 1.5707288f);
					ret = ret * sqrt_onemx;
					
					// Convert to units of π using FMA: 0.5 - ret * (1/π) = -ret * (1/π) + 0.5
					ret = Single.FusedMultiplyAdd(-ret, 0.31830988618379067f, 0.5f);
					return Single.CopySign(ret, x);
				}
			}
			""";
	}

	private static string GenerateFastAsinPiMethodDouble()
	{
		return """
			private static double FastAsinPi(double x)
			{
				// Clamp input to valid range [-1, 1]
				if (x < -1.0) x = -1.0;
				if (x > 1.0) x = 1.0;
				
				// For AsinPi, we compute asin(x)/π
				// Use a better polynomial approximation based on sqrt(1-x) for better accuracy
				var xa = Double.Abs(x);
				
				// For small values, use Taylor series: asin(x)/π ≈ x/π + x³/(6π) + ...
				// For larger values, use sqrt-based approximation
				if (xa < 0.5)
				{
					// Taylor series approach for small values
					var x2 = xa * xa;
					var ret = 0.16666666666666666;  // 1/6
					ret = Double.FusedMultiplyAdd(ret, x2, 1.0);
					ret = ret * xa * 0.31830988618379067;  // 1/π
					return Double.CopySign(ret, x);
				}
				else
				{
					// sqrt-based approximation for larger values
					// asin(x) ≈ π/2 - sqrt(1-x) * (c0 + c1*x + c2*x²)
					var onemx = 1.0 - xa;
					var sqrt_onemx = Double.Sqrt(onemx);
					
					var ret = -0.0187293;
					ret = Double.FusedMultiplyAdd(ret, xa, 0.0742610);
					ret = Double.FusedMultiplyAdd(ret, xa, -0.2121144);
					ret = Double.FusedMultiplyAdd(ret, xa, 1.5707288);
					ret = ret * sqrt_onemx;
					
					// Convert to units of π using FMA: 0.5 - ret * (1/π) = -ret * (1/π) + 0.5
					ret = Double.FusedMultiplyAdd(-ret, 0.31830988618379067, 0.5);
					return Double.CopySign(ret, x);
				}
			}
			""";
	}
}

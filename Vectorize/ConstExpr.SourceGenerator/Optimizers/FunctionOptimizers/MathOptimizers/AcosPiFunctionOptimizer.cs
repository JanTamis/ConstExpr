using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AcosPiFunctionOptimizer() : BaseMathFunctionOptimizer("AcosPi", 1)
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
				? GenerateFastAcosPiMethodFloat()
				: GenerateFastAcosPiMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastAcosPi", parameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateFastAcosPiMethodFloat()
	{
		return """
			private static float FastAcosPi(float x)
			{
				// Clamp input to valid range [-1, 1]
				if (x < -1.0f) x = -1.0f;
				if (x > 1.0f) x = 1.0f;
				
				// Fast approximation using polynomial with FMA, directly computing result in units of π
				var negate = x < 0.0f ? 1.0f : 0.0f;
				x = x < 0.0f ? -x : x;
				
				// Polynomial coefficients adjusted for π-scaled output
				// These are the original coefficients divided by π
				var ret = -0.00596227f;  // -0.0187293 / π
				ret = Single.FusedMultiplyAdd(ret, x, 0.0236338f);  // 0.0742610 / π
				ret = Single.FusedMultiplyAdd(ret, x, -0.0675189f);  // -0.2121144 / π
				ret = Single.FusedMultiplyAdd(ret, x, 0.5f);  // 1.5707288 / π ≈ 0.5
				ret = ret * Single.Sqrt(1.0f - x);
				ret = Single.FusedMultiplyAdd(-2.0f * negate, ret, ret);  // ret - 2.0 * negate * ret using FMA
				
				return Single.FusedMultiplyAdd(negate, 1.0f, ret);
			}
			""";
	}

	private static string GenerateFastAcosPiMethodDouble()
	{
		return """
			private static double FastAcosPi(double x)
			{
				// Clamp input to valid range [-1, 1]
				if (x < -1.0) x = -1.0;
				if (x > 1.0) x = 1.0;
				
				// Fast approximation using polynomial with FMA, directly computing result in units of π
				var negate = x < 0.0 ? 1.0 : 0.0;
				x = x < 0.0 ? -x : x;
				
				// Polynomial coefficients adjusted for π-scaled output
				// These are the original coefficients divided by π
				var ret = -0.0059622704862860465;  // -0.0187293 / π
				ret = Double.FusedMultiplyAdd(ret, x, 0.023633778501171472);  // 0.0742610 / π
				ret = Double.FusedMultiplyAdd(ret, x, -0.067518943563376579);  // -0.2121144 / π
				ret = Double.FusedMultiplyAdd(ret, x, 0.5);  // π/2 / π = 0.5
				ret = ret * Double.Sqrt(1.0 - x);
				ret = Double.FusedMultiplyAdd(-2.0 * negate, ret, ret);  // ret - 2.0 * negate * ret using FMA
				
				return Double.FusedMultiplyAdd(negate, 1.0, ret);
			}
			""";
	}
}

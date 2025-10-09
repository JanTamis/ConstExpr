using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class AcosFunctionOptimizer() : BaseFunctionOptimizer("Acos", 1)
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		// When FastMath is enabled, add a fast acos approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath
			&& paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastAcosMethodFloat()
				: GenerateFastAcosMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastAcos", parameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateFastAcosMethodFloat()
	{
		return """
			private static float FastAcos(float x)
			{
				// Clamp input to valid range [-1, 1] using manual if checks for optimal performance
				if (x < -1.0f) x = -1.0f;
				if (x > 1.0f) x = 1.0f;
				
				// Fast approximation using polynomial with FMA
				var negate = x < 0.0f ? 1.0f : 0.0f;
				x = x < 0.0f ? -x : x;
				
				// Use FMA for polynomial evaluation - more accurate and potentially faster
				var ret = -0.0187293f;
				ret = Single.FusedMultiplyAdd(ret, x, 0.0742610f);
				ret = Single.FusedMultiplyAdd(ret, x, -0.2121144f);
				ret = Single.FusedMultiplyAdd(ret, x, 1.5707288f);
				ret = ret * Single.Sqrt(1.0f - x);
				ret = ret - 2.0f * negate * ret;
				
				return Single.FusedMultiplyAdd(negate, 3.14159265f, ret);
			}
			""";
	}

	private static string GenerateFastAcosMethodDouble()
	{
		return """
			private static double FastAcos(double x)
			{
				// Clamp input to valid range [-1, 1] using manual if checks for optimal performance
				if (x < -1.0) x = -1.0;
				if (x > 1.0) x = 1.0;
				
				// Fast approximation using polynomial with FMA
				var negate = x < 0.0 ? 1.0 : 0.0;
				x = x < 0.0 ? -x : x;
				
				// Use FMA for polynomial evaluation - more accurate and potentially faster
				var ret = -0.0187293;
				ret = Double.FusedMultiplyAdd(ret, x, 0.0742610);
				ret = Double.FusedMultiplyAdd(ret, x, -0.2121144);
				ret = Double.FusedMultiplyAdd(ret, x, 1.5707288);
				ret = ret * Double.Sqrt(1.0 - x);
				ret = ret - 2.0 * negate * ret;
				
				return Double.FusedMultiplyAdd(negate, 3.14159265358979323846, ret);
			}
			""";
	}
}

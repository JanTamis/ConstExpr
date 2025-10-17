using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class AsinhFunctionOptimizer() : BaseMathFunctionOptimizer("Asinh", 1)
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
				? GenerateFastAsinhMethodFloat()
				: GenerateFastAsinhMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastAsinh", parameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateFastAsinhMethodFloat()
	{
		return """
			private static float FastAsinh(float x)
			{
				// Optimized for speed: use simpler approximation with fewer branches
				var xa = Single.Abs(x);
				
				// For very small values, use simple Taylor: asinh(x) ≈ x
				if (xa < 0.1f)
				{
					return x; // Error < 0.0017 for |x| < 0.1
				}
				
				// For large values, use fast approximation: asinh(x) ≈ sign(x) * ln(2|x|)
				if (xa > 10.0f)
				{
					var result = Single.Log(xa + xa); // ln(2x) is faster
					return Single.CopySign(result, x);
				}
				
				// For medium values, use direct formula
				var result2 = Single.Log(xa + Single.Sqrt(Single.FusedMultiplyAdd(xa, xa, 1.0f)));
				return Single.CopySign(result2, x);
			}
			""";
	}

	private static string GenerateFastAsinhMethodDouble()
	{
		return """
			private static double FastAsinh(double x)
			{
				// Optimized for speed: use simpler approximation with fewer branches
				var xa = Double.Abs(x);
				
				// For very small values, use simple Taylor: asinh(x) ≈ x
				if (xa < 0.1)
				{
					return x; // Error < 0.0017 for |x| < 0.1
				}
				
				// For large values, use fast approximation: asinh(x) ≈ sign(x) * ln(2|x|)
				if (xa > 10.0)
				{
					var result = Double.Log(xa + xa); // ln(2x) is faster
					return Double.CopySign(result, x);
				}
				
				// For medium values, use direct formula
				var result2 = Double.Log(xa + Double.Sqrt(Double.FusedMultiplyAdd(xa, xa, 1.0)));
				return Double.CopySign(result2, x);
			}
			""";
	}
}

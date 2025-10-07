using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class CosPiFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "CosPi")
		{
			return false;
		}

		var containing = method.ContainingType?.ToString();
		var paramType = method.Parameters.Length > 0 ? method.Parameters[0].Type : null;
		var containingName = method.ContainingType?.Name;
		var paramTypeName = paramType?.Name;

		var isMath = containing is "System.Math" or "System.MathF";
		var isNumericHelper = paramTypeName is not null && containingName == paramTypeName;

		if (!isMath && !isNumericHelper || paramType is null)
		{
			return false;
		}

		if (!paramType.IsNumericType())
		{
			return false;
		}

		// When FastMath is enabled, add a fast cospi approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			// Generate fast cospi method for floating point types
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastCosPiMethodFloat() 
					: GenerateFastCosPiMethodDouble();
					
				var fastCosPiMethod = ParseMethodFromString(methodString);
				
				if (fastCosPiMethod is not null)
				{
					if (!additionalMethods.ContainsKey(fastCosPiMethod))
					{
						additionalMethods.Add(fastCosPiMethod, false);
					}
					
					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastCosPi"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));
					
					return true;
				}
			}
		}

		result = CreateInvocation(paramType, "CosPi", parameters);
		return true;
	}

	private static string GenerateFastCosPiMethodFloat()
	{
		return """
			private static float FastCosPi(float x)
			{
				// Fast cosine(π*x) approximation using optimized minimax polynomial
				// CosPi(x) = Cos(π*x)
				
				// Range reduction: bring x to [-1, 1]
				x = x - Single.Floor(x / 2.0f) * 2.0f;
				if (x > 1.0f) x -= 2.0f;
				if (x < -1.0f) x += 2.0f;
				
				// Use symmetry: cos(π*(-x)) = cos(π*x)
				x = Single.Abs(x);
				
				// For better accuracy, split into two ranges
				if (x <= 0.5f)
				{
					// For x in [0, 0.5], use polynomial directly
					var px = x * Single.Pi;
					var px2 = px * px;
					var ret = 0.0003538394f;
					ret = Single.FusedMultiplyAdd(ret, px2, -0.0041666418f);
					ret = Single.FusedMultiplyAdd(ret, px2, 0.041666666f);
					ret = Single.FusedMultiplyAdd(ret, px2, -0.5f);
					ret = Single.FusedMultiplyAdd(ret, px2, 1.0f);
					return ret;
				}
				
				// For x in (0.5, 1], use cos(π*x) = -cos(π*(1-x))
				var px = (1.0f - x) * Single.Pi;
				var px2 = px * px;
				var ret = 0.0003538394f;
				ret = Single.FusedMultiplyAdd(ret, px2, -0.0041666418f);
				ret = Single.FusedMultiplyAdd(ret, px2, 0.041666666f);
				ret = Single.FusedMultiplyAdd(ret, px2, -0.5f);
				ret = Single.FusedMultiplyAdd(ret, px2, 1.0f);
				return -ret;
			}
			""";
	}

	private static string GenerateFastCosPiMethodDouble()
	{
		return """
			private static double FastCosPi(double x)
			{
				// Fast cosine(π*x) approximation for double precision
				// CosPi(x) = Cos(π*x)
				
				// Range reduction: bring x to [-1, 1]
				x = x - Double.Floor(x / 2.0) * 2.0;
				if (x > 1.0) x -= 2.0;
				if (x < -1.0) x += 2.0;
				
				// Use symmetry: cos(π*(-x)) = cos(π*x)
				x = Double.Abs(x);
				
				// For better accuracy, split into two ranges
				if (x <= 0.5)
				{
					// For x in [0, 0.5], use polynomial directly
					var px = x * Double.Pi;
					var px2 = px * px;
					var ret = -1.1940250944959890e-7;
					ret = Double.FusedMultiplyAdd(ret, px2, 2.0876755527587203e-5);
					ret = Double.FusedMultiplyAdd(ret, px2, -0.0013888888888739916);
					ret = Double.FusedMultiplyAdd(ret, px2, 0.041666666666666602);
					ret = Double.FusedMultiplyAdd(ret, px2, -0.5);
					ret = Double.FusedMultiplyAdd(ret, px2, 1.0);
					return ret;
				}
				
				// For x in (0.5, 1], use cos(π*x) = -cos(π*(1-x))
				var px = (1.0 - x) * Double.Pi;
				var px2 = px * px;
				var ret = -1.1940250944959890e-7;
				ret = Double.FusedMultiplyAdd(ret, px2, 2.0876755527587203e-5);
				ret = Double.FusedMultiplyAdd(ret, px2, -0.0013888888888739916);
				ret = Double.FusedMultiplyAdd(ret, px2, 0.041666666666666602);
				ret = Double.FusedMultiplyAdd(ret, px2, -0.5);
				ret = Double.FusedMultiplyAdd(ret, px2, 1.0);
				return -ret;
			}
			""";
	}
}

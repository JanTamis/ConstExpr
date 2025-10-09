using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class SqrtFunctionOptimizer() : BaseFunctionOptimizer("Sqrt", 1)
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		// When FastMath is enabled, add a fast sqrt approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath
			&& paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastSqrtMethodFloat()
				: GenerateFastSqrtMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastSqrt", parameters);
			return true;
		}

		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateFastSqrtMethodFloat()
	{
		return """
			private static float FastSqrt(float x)
			{
				if (x <= 0.0f)
					return 0.0f;
				
				// Direct square root approximation using bit manipulation
				var i = BitConverter.SingleToInt32Bits(x);
				i = 0x1fbd1df5 + (i >> 1);
				var y = BitConverter.Int32BitsToSingle(i);
				
				// Newton-Raphson iteration: y = (y + x/y) / 2
				// Pre-compute for better performance
				var halfY = y * 0.5f;
				var halfXdivY = 0.5f * x / y;
				return halfY + halfXdivY;
			}
			""";
	}

	private static string GenerateFastSqrtMethodDouble()
	{
		return """
			private static double FastSqrt(double x)
			{
				if (x <= 0.0)
					return 0.0;
				
				// Direct square root approximation using bit manipulation
				var i = BitConverter.DoubleToInt64Bits(x);
				i = 0x5fe6ec85e7de30daL + (i >> 1);
				var y = BitConverter.Int64BitsToDouble(i);
				
				// Newton-Raphson iteration: y = (y + x/y) / 2
				// Pre-compute for better performance
				var halfY = y * 0.5;
				var halfXdivY = 0.5 * x / y;
				return halfY + halfXdivY;
			}
			""";
	}
}
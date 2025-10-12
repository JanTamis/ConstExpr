using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class SqrtFunctionOptimizer() : BaseFunctionOptimizer("Sqrt", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMethod(method, out var paramType))
		{
			return false;
		}

		// // When FastMath is enabled, add a fast sqrt approximation method
		// if (floatingPointMode == FloatingPointEvaluationMode.FastMath
		// 	&& paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		// {
		// 	var methodString = paramType.SpecialType == SpecialType.System_Single
		// 		? GenerateFastSqrtMethodFloat()
		// 		: GenerateFastSqrtMethodDouble();
		//
		// 	additionalMethods.TryAdd(ParseMethodFromString(methodString), false);
		//
		// 	result = CreateInvocation("FastSqrt", parameters);
		// 	return true;
		// }

		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	// private static string GenerateFastSqrtMethodFloat()
	// {
	// 	return """
	// 		private static float FastSqrt(float x)
	// 		{
	// 			if (x <= 0.0f)
	// 				return 0.0f;
	// 			
	// 			var i = BitConverter.SingleToInt32Bits(x);
	// 			i = 0x5f375a86 - (i >> 1); // Magic constant for inverse sqrt
	// 			var y = BitConverter.Int32BitsToSingle(i);
	// 			
	// 			// One Newton-Raphson iteration for better precision
	// 			y = Single.FusedMultiplyAdd(-0.5F * x * y, y, 1.5F) * y;
	// 			
	// 			return x * y; // Convert from inverse sqrt to sqrt
	// 		}
	// 		""";
	// }
	//
	// private static string GenerateFastSqrtMethodDouble()
	// {
	// 	return """
	// 		private static double FastSqrt(double x)
	// 		{
	// 			if (x <= 0D)
	// 				return 0D;
	// 		
	// 			var i = BitConverter.DoubleToInt64Bits(x);
	// 			i = 0x5fe6ec85e7de30daL - (i >> 1); // Correct magic constant
	// 			var y = BitConverter.Int64BitsToDouble(i);
	// 		
	// 			// One Newton-Raphson iteration for better precision
	// 			y = Double.FusedMultiplyAdd(-0.5 * x * y, y, 1.5) * y;
	// 		
	// 			return x * y; // Convert from inverse sqrt to sqrt
	// 		}
	// 		""";
	// }
}
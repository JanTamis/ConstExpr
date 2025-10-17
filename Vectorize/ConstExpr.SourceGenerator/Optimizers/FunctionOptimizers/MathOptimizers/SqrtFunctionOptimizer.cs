using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class SqrtFunctionOptimizer() : BaseMathFunctionOptimizer("Sqrt", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(method, out var paramType))
		{
			return false;
		}

		//// When FastMath is enabled, add a fast sqrt approximation method
		//if (invocation.Parent is CastExpressionSyntax
		//	{
		//		Type: PredefinedTypeSyntax
		//		{
		//			Keyword.RawKind: (int)SyntaxKind.IntKeyword
		//				or (int)SyntaxKind.UIntKeyword
		//				or (int)SyntaxKind.LongKeyword
		//				or (int)SyntaxKind.ULongKeyword
		//				or (int)SyntaxKind.ShortKeyword
		//				or (int)SyntaxKind.UShortKeyword
		//				or (int)SyntaxKind.ByteKeyword
		//				or (int)SyntaxKind.SByteKeyword
		//				or (int)SyntaxKind.CharKeyword
		//		}
		//	}
		//	&& paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		//{
		//	var methodString = paramType.SpecialType == SpecialType.System_Single
		//		? GenerateFastSqrtMethodFloat()
		//		: GenerateFastSqrtMethodDouble();

		//	additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

		//	result = CreateInvocation("FastSqrt", parameters);
		//	return true;
		//}

		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	//private static string GenerateFastSqrtMethodFloat()
	//{
	//	return """
	// 		private static float FastSqrt(float x)
	// 		{
	// 			if (x <= 0.0f)
	// 				return 0.0f;

	// 			var i = BitConverter.SingleToInt32Bits(x);
	// 			i = 0x5f375a86 - (i >> 1); // Magic constant for inverse sqrt

	// 			return x * BitConverter.Int32BitsToSingle(i); // Convert from inverse sqrt to sqrt
	// 		}
	// 		""";
	//}

	//private static string GenerateFastSqrtMethodDouble()
	//{
	//	return """
	// 		private static double FastSqrt(double x)
	// 		{
	// 			if (x <= 0D)
	// 				return 0D;

	// 			var i = BitConverter.DoubleToInt64Bits(x);
	// 			i = 0x5fe6ec85e7de30daL - (i >> 1); // Magic constant for inverse sqrt

	// 			return x * BitConverter.Int64BitsToDouble(i); // Convert from inverse sqrt to sqrt
	// 		}
	// 		""";
	//}
}
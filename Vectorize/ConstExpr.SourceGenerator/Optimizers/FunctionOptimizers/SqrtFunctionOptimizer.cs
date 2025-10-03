using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class SqrtFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, ISet<SyntaxNode> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Sqrt")
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

		// When FastMath is enabled, add a fast sqrt approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			// Generate fast sqrt method for floating point types
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastSqrtMethodFloat() 
					: GenerateFastSqrtMethodDouble();
					
				var fastSqrtMethod = ParseMethodFromString(methodString);
				
				if (fastSqrtMethod is not null)
				{
					additionalMethods.Add(fastSqrtMethod);
					
					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastSqrt"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));
					
					return true;
				}
			}
		}

		result = CreateInvocation(paramType, "Sqrt", parameters);
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
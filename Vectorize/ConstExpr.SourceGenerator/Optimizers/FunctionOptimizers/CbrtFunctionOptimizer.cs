using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class CbrtFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "Cbrt")
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

		// When FastMath is enabled, add a fast cbrt approximation method
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			// Generate fast cbrt method for floating point types
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
					? GenerateFastCbrtMethodFloat() 
					: GenerateFastCbrtMethodDouble();
					
				var fastCbrtMethod = ParseMethodFromString(methodString);
				
				if (fastCbrtMethod is not null)
				{
					if (!additionalMethods.ContainsKey(fastCbrtMethod))
					{
						additionalMethods.Add(fastCbrtMethod, false);
					}
					
					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastCbrt"))
						.WithArgumentList(
							SyntaxFactory.ArgumentList(
								SyntaxFactory.SeparatedList(
									parameters.Select(SyntaxFactory.Argument))));
					
					return true;
				}
			}
		}

		result = CreateInvocation(paramType, "Cbrt", parameters);
		return true;
	}

	private static string GenerateFastCbrtMethodFloat()
	{
		return """
			private static float FastCbrt(float x)
			{
				if (x == 0.0f)
					return 0.0f;
				
				var absX = System.Math.Abs(x);
				
				// Initial approximation using bit manipulation
				var i = BitConverter.SingleToInt32Bits(absX);
				i = 0x2a517d47 + i / 3;
				var y = BitConverter.Int32BitsToSingle(i);
				
				// Newton-Raphson iteration: y = (2*y + x/y²) / 3
				y = (y + y + absX / (y * y)) / 3.0f;
				y = (y + y + absX / (y * y)) / 3.0f;
				
				return System.MathF.CopySign(y, x);
			}
			""";
	}

	private static string GenerateFastCbrtMethodDouble()
	{
		return """
			private static double FastCbrt(double x)
			{
				if (x == 0.0)
					return 0.0;
				
				var absX = System.Math.Abs(x);
				
				// Initial approximation using bit manipulation
				var i = BitConverter.DoubleToInt64Bits(absX);
				i = 0x2a9f8b7cef1d0da0L + i / 3;
				var y = BitConverter.Int64BitsToDouble(i);
				
				// Newton-Raphson iteration: y = (2*y + x/y²) / 3
				y = (y + y + absX / (y * y)) / 3.0;
				y = (y + y + absX / (y * y)) / 3.0;
				
				return System.Math.CopySign(y, x);
			}
			""";
	}
}

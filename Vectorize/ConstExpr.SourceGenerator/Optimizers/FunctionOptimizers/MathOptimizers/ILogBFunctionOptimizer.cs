using System;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class ILogBFunctionOptimizer() : BaseMathFunctionOptimizer("ILogB", 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(method, out var paramType))
		{
			return false;
		}

		if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
		{
			var methodString = paramType.SpecialType == SpecialType.System_Single
				? GenerateFastILogBMethodFloat()
				: GenerateFastILogBMethodDouble();

			additionalMethods.TryAdd(ParseMethodFromString(methodString), false);

			result = CreateInvocation("FastILogB", parameters);
			return true;
		}

		// Default: keep as ILogB call (target numeric helper type)
		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}

	private static string GenerateFastILogBMethodFloat()
	{
		return """
			private static int FastILogB(float x)
			{
				var bits = BitConverter.SingleToInt32Bits(x);
				var exp = (bits >> 23) & 0xFF;

				// Fast path for normal numbers (most common case)
				if (exp is not 0 and not 0x7FF)
				{
						return exp - 127;
				}
				
				// Handle special cases
				if (exp == 0xFF) return Int32.MaxValue; // NaN or Infinity
				if (x == 0.0f) return Int32.MinValue; // Zero

				// Subnormal
				return -126;
			}
			""";
	}

	private static string GenerateFastILogBMethodDouble()
	{
		return """
			private static int FastILogB(double x)
			{
				var bits = BitConverter.DoubleToInt64Bits(x);
				var exp = (int)((bits >> 52) & 0x7FF);

				// Fast path for normal numbers (most common case)
				if (exp is not 0 and not 0x7FF)
				{
						return exp - 1023;
				}

				// Handle special cases
				if (exp == 0x7FF) return Int32.MaxValue; // NaN or Infinity
				if (x == 0.0) return Int32.MinValue; // Zero

				// Subnormal
				return -1022;
			}
			""";
	}
}

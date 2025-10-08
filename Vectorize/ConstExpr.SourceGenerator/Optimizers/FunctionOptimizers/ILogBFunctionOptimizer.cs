using ConstExpr.Core.Attributes;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers;

public class ILogBFunctionOptimizer : BaseFunctionOptimizer
{
	public override bool TryOptimize(IMethodSymbol method, FloatingPointEvaluationMode floatingPointMode, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (method.Name != "ILogB")
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

		// ILogB has a single parameter (x)
		if (parameters.Count != 1)
		{
			return false;
		}

		var x = parameters[0];

		// When FastMath enabled, add fast ilogb implementation for float/double
		if (floatingPointMode == FloatingPointEvaluationMode.FastMath)
		{
			if (paramType.SpecialType is SpecialType.System_Single or SpecialType.System_Double)
			{
				var methodString = paramType.SpecialType == SpecialType.System_Single
						? GenerateFastILogBMethodFloat()
						: GenerateFastILogBMethodDouble();

				var fastMethod = ParseMethodFromString(methodString);

				if (fastMethod is not null)
				{
					if (!additionalMethods.ContainsKey(fastMethod))
					{
						additionalMethods.Add(fastMethod, false);
					}

					result = SyntaxFactory.InvocationExpression(
						SyntaxFactory.IdentifierName("FastILogB"))
							.WithArgumentList(
								SyntaxFactory.ArgumentList(
									SyntaxFactory.SeparatedList(parameters.Select(SyntaxFactory.Argument))));

					return true;
				}
			}
		}

		// Default: keep as ILogB call (target numeric helper type)
		result = CreateInvocation(paramType, "ILogB", x);
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

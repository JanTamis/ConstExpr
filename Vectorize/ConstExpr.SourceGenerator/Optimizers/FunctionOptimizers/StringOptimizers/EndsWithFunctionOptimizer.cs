using System;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.StringOptimizers;

/// <summary>
/// Optimizer for string.EndsWith(char) calls.
/// </summary>
public class EndsWithFunctionOptimizer(SyntaxNode? instance) : BaseStringFunctionOptimizer(instance, "EndsWith", false, n => n is 1)
{
	protected override bool TryOptimizeString(FunctionOptimizerContext context, ITypeSymbol stringType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		// Check for instance string
		if (!TryGetStringInstance(out var instanceString))
		{
			return false;
		}

		if (context.Method.Parameters[0].Type.SpecialType == SpecialType.System_Char)
		{
			if (String.IsNullOrEmpty(instanceString))
			{
				result = CreateLiteral(false);
				return true;
			}

			result = EqualsExpression(CreateLiteral(instanceString![^1]), context.VisitedParameters[0]);
			return true;
		}

		return false;
	}
}
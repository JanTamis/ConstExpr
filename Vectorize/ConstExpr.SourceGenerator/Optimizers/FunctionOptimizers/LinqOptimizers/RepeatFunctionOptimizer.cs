using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
///   Optimizer for Enumerable.Repeat(element, count).
///   Optimizes patterns such as:
///   - Enumerable.Repeat(element, 0) => [] (empty sequence, regardless of element value)
///   - Enumerable.Repeat(element, 1) => [element] (single-element collection expression)
/// </summary>
public class RepeatFunctionOptimizer() : BaseLinqFunctionOptimizer("Repeat", n => n is 2)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		if (!IsValidLinqMethod(context))
		{
			return false;
		}

		var elementExpr = context.VisitedParameters[0];
		var countExpr = context.VisitedParameters[1];

		switch (countExpr)
		{
			// Enumerable.Repeat(element, 0) => [] (empty, regardless of element)
			case LiteralExpressionSyntax { Token.Value: 0 }:
			{
				result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
				return true;
			}
			// Enumerable.Repeat(element, 1) => [element]
			case LiteralExpressionSyntax { Token.Value: 1 }:
			{
				result = CreateCollection(elementExpr);
				return true;
			}
		}

		return false;
	}
}
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SkipLast context.Method.
/// Optimizes patterns such as:
/// - collection.SkipLast(0) => collection (skip nothing)
/// - collection.SkipLast(n).SkipLast(m) => collection.SkipLast(n + m)
/// </summary>
public class SkipLastFunctionOptimizer() : BaseLinqFunctionOptimizer("SkipLast", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		// Optimize SkipLast(0) => source (skip nothing)
		if (context.VisitedParameters[0] is LiteralExpressionSyntax { Token.Value: <= 0 })
		{
			result = context.Visit(source) ?? source;
			return true;
		}

		result = null;
		return false;
	}
}


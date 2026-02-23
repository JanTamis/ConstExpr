using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.TakeLast context.Method.
/// Optimizes patterns such as:
/// - collection.TakeLast(0) => Enumerable.Empty&lt;T&gt;() (take nothing)
/// </summary>
public class TakeLastFunctionOptimizer() : BaseLinqFunctionOptimizer("TakeLast", 1)
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

		// Optimize TakeLast(0) => Enumerable.Empty<T>()
		if (context.VisitedParameters[0] is LiteralExpressionSyntax { Token.Value: <= 0 })
		{
			result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}
}


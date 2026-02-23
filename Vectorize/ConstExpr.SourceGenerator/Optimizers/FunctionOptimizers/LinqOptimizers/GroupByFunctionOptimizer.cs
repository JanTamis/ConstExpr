using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.GroupBy context.Method.
/// Optimizes patterns such as:
/// - Enumerable.Empty&lt;T&gt;().GroupBy(selector) => Enumerable.Empty&lt;IGrouping&lt;TKey, T&gt;&gt;()
/// </summary>
public class GroupByFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.GroupBy), 1, 2, 3)
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

		// Optimize Enumerable.Empty<T>().GroupBy(selector) => Enumerable.Empty<IGrouping<TKey, T>>()
		if (IsEmptyEnumerable(context.Visit(source) ?? source) && context.Method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } returnType)
		{
			result = CreateEmptyEnumerableCall(returnType.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}
}


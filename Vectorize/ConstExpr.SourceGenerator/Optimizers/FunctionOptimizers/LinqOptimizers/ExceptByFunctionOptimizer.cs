using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ExceptBy context.Method.
/// Optimizes patterns such as:
/// - collection.ExceptBy(Enumerable.Empty&lt;T&gt;(), selector) => collection.DistinctBy(selector)
/// - Enumerable.Empty&lt;T&gt;().ExceptBy(collection, selector) => Enumerable.Empty&lt;T&gt;()
/// </summary>
public class ExceptByFunctionOptimizer() : BaseLinqFunctionOptimizer("ExceptBy", 2)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, context.SymbolStore, out result, out source))
		{
			return true;
		}

		var secondSource = context.VisitedParameters[0];

		// Optimize Enumerable.Empty<T>().ExceptBy(collection, selector) => Enumerable.Empty<T>()
		if (IsEmptyEnumerable(source))
		{
			result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
			return true;
		}

		// Optimize collection.ExceptBy(Enumerable.Empty<TKey>(), selector) => collection.DistinctBy(selector)
		// (removing nothing means just keeping unique keys)
		if (IsEmptyEnumerable(secondSource))
		{
			result = TryOptimizeByOptimizer<DistinctByFunctionOptimizer>(context, CreateInvocation(source, "DistinctBy", context.OriginalParameters[1]));
			return true;
		}

		result = null;
		return false;
	}
}


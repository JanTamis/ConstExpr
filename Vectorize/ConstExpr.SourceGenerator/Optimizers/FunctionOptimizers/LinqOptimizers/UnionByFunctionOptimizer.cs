using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.UnionBy context.Method.
/// Optimizes patterns such as:
/// - collection.UnionBy(Enumerable.Empty&lt;T&gt;(), selector) => collection.DistinctBy(selector)
/// - Enumerable.Empty&lt;T&gt;().UnionBy(collection, selector) => collection.DistinctBy(selector)
/// - collection.UnionBy(collection, selector) => collection.DistinctBy(selector) (same source)
/// </summary>
public class UnionByFunctionOptimizer() : BaseLinqFunctionOptimizer("UnionBy", 2)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		var secondSource = context.VisitedParameters[0];
		
		// Optimize collection.UnionBy(Enumerable.Empty<T>(), selector) => collection.DistinctBy(selector)
		if (IsEmptyEnumerable(secondSource))
		{
			result = TryOptimizeByOptimizer<DistinctByFunctionOptimizer>(context, CreateInvocation(source, "DistinctBy", context.OriginalParameters[1]));
			return true;
		}

		// Optimize Enumerable.Empty<T>().UnionBy(collection, selector) => collection.DistinctBy(selector)
		if (IsEmptyEnumerable(source))
		{
			result = TryOptimizeByOptimizer<DistinctByFunctionOptimizer>(context, CreateInvocation(secondSource, "DistinctBy", context.OriginalParameters[1]));
			return true;
		}

		// Optimize collection.UnionBy(collection, selector) => collection.DistinctBy(selector) (same reference)
		if (AreSyntacticallyEquivalent(source, secondSource))
		{
			result = TryOptimizeByOptimizer<DistinctByFunctionOptimizer>(context, CreateInvocation(source, "DistinctBy", context.OriginalParameters[1]));
			return true;
		}

		result = null;
		return false;
	}
}


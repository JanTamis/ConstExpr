using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Sum method.
/// Optimizes patterns such as:
/// - collection.Sum(x => x) => collection.Sum() (identity lambda removal)
/// - collection.Select(x => x.Property).Sum() => collection.Sum(x => x.Property)
/// - collection.OrderBy(...).Sum() => collection.Sum() (ordering doesn't affect sum)
/// - collection.AsEnumerable().Sum() => collection.Sum()
/// - collection.ToList().Sum() => collection.Sum()
/// - collection.Reverse().Sum() => collection.Sum()
/// </summary>
public class SumFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Sum), 0, 1)
{
	// Operations that don't affect the sum
	private static readonly HashSet<string> OperationsThatDontAffectSum =
	[
		nameof(Enumerable.AsEnumerable),
		nameof(Enumerable.ToList),
		nameof(Enumerable.ToArray),
		nameof(Enumerable.OrderBy),
		nameof(Enumerable.OrderByDescending),
		"Order",
		"OrderDescending",
		nameof(Enumerable.ThenBy),
		nameof(Enumerable.ThenByDescending),
		nameof(Enumerable.Reverse),
	];

	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip operations that don't affect sum
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectSum, out source);

		// Optimize Sum(x => x) => Sum() (identity lambda removal)
		if (parameters.Count == 1
		    && TryGetLambda(parameters[0], out var lambda)
		    && IsIdentityLambda(lambda))
		{
			result = CreateSimpleInvocation(visit(source) ?? source, nameof(Enumerable.Sum));
			return true;
		}

		// Optimize source.Select(selector).Sum() => source.Sum(selector)
		if (parameters.Count == 0
		    && IsLinqMethodChain(source, nameof(Enumerable.Select), out var selectInvocation)
		    && TryGetLinqSource(selectInvocation, out var selectSource)
		    && selectInvocation.ArgumentList.Arguments.Count == 1)
		{
			TryGetOptimizedChainExpression(selectSource, OperationsThatDontAffectSum, out selectSource);
			
			var selector = selectInvocation.ArgumentList.Arguments[0].Expression;
			result = CreateInvocation(visit(selectSource) ?? selectSource, nameof(Enumerable.Sum), visit(selector) ?? selector);
			return true;
		}

		// If we skipped any operations, create optimized Sum() call
		if (isNewSource)
		{
			result = CreateInvocation(visit(source) ?? source, nameof(Enumerable.Sum), parameters);
			return true;
		}

		result = null;
		return false;
	}
}

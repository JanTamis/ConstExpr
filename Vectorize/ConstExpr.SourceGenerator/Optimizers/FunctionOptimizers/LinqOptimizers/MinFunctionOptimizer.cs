using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Min method.
/// Optimizes patterns such as:
/// - collection.Min(x => x) => collection.Min() (identity lambda removal)
/// - collection.Select(x => x.Property).Min() => collection.Min(x => x.Property)
/// - collection.OrderBy(...).Min() => collection.Min() (ordering doesn't affect min)
/// - collection.AsEnumerable().Min() => collection.Min()
/// - collection.ToList().Min() => collection.Min()
/// </summary>
public class MinFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Min), 0, 1)
{
	// Operations that don't affect the minimum value
	private static readonly HashSet<string> OperationsThatDontAffectMin =
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

		// Recursively skip operations that don't affect min
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectMin, out source);

		// Optimize Min(x => x) => Min() (identity lambda removal)
		if (parameters.Count == 1
		    && TryGetLambda(parameters[0], out var lambda)
		    && IsIdentityLambda(visit(lambda) as LambdaExpressionSyntax ?? lambda))
		{
			result = CreateSimpleInvocation(visit(source) ?? source, nameof(Enumerable.Min));
			return true;
		}

		// Optimize source.Select(selector).Min() => source.Min(selector)
		if (parameters.Count == 0
		    && IsLinqMethodChain(source, nameof(Enumerable.Select), out var selectInvocation)
		    && TryGetLinqSource(selectInvocation, out var selectSource)
		    && selectInvocation.ArgumentList.Arguments.Count == 1)
		{
			TryGetOptimizedChainExpression(selectSource, OperationsThatDontAffectMin, out selectSource);
			
			var selector = selectInvocation.ArgumentList.Arguments[0].Expression;
			result = CreateInvocation(visit(selectSource) ?? selectSource, nameof(Enumerable.Min), visit(selector) ?? selector);
			return true;
		}

		// If we skipped any operations, create optimized Min() call
		if (isNewSource && parameters.Count == 0)
		{
			result = CreateSimpleInvocation(visit(source) ?? source, nameof(Enumerable.Min));
			return true;
		}

		result = null;
		return false;
	}
}
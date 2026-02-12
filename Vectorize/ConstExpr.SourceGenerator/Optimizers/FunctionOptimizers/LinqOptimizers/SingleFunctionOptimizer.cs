using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Single context.Method.
/// Optimizes patterns such as:
/// - collection.Where(predicate).Single() => collection.Single(predicate)
/// - collection.AsEnumerable().Single() => collection.Single()
/// - collection.ToList().Single() => collection.Single()
/// </summary>
public class SingleFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Single), 0, 1)
{
	// Operations that don't affect which element is "single"
	private static readonly HashSet<string> OperationsThatDontAffectSingle =
	[
		nameof(Enumerable.AsEnumerable),
		nameof(Enumerable.ToList),
		nameof(Enumerable.ToArray),
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		// Recursively skip operations that don't affect single
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectSingle, out source);

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		// Optimize source.Where(predicate).Single() => source.Single(predicate)
		if (context.VisitedParameters.Count == 0
		    && IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		    && TryGetLinqSource(whereInvocation, out var whereSource)
		    && whereInvocation.ArgumentList.Arguments.Count == 1)
		{
			TryGetOptimizedChainExpression(whereSource, OperationsThatDontAffectSingle, out whereSource);
			
			var predicate = whereInvocation.ArgumentList.Arguments[0].Expression;
			result = CreateInvocation(context.Visit(whereSource) ?? whereSource, nameof(Enumerable.Single), context.Visit(predicate) ?? predicate);
			return true;
		}

		// If we skipped any operations, create optimized Single() call
		if (isNewSource)
		{
			result = CreateInvocation(context.Visit(source) ?? source, nameof(Enumerable.Single), context.VisitedParameters);
			return true;
		}

		result = null;
		return false;
	}
}

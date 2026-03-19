using System;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ToArray context.Method.
/// Optimizes patterns such as:
/// - collection.ToArray().ToArray() => collection.ToArray() (redundant ToArray)
/// - collection.ToList().ToArray() => collection.ToArray()
/// - collection.AsEnumerable().ToArray() => collection.ToArray()
/// - arr.Where(p).ToArray() => Array.FindAll(arr, p) (direct BCL call, no LINQ pipeline)
/// - arr.Select(f).Where(p).ToArray() => Array.FindAll(arr, x => p(f(x))) (fused selector+predicate)
/// </summary>
public class ToArrayFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ToArray), 0)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		var isNewSource = TryGetOptimizedChainExpression(source, MaterializingMethods, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Where(p).ToArray() → Array.FindAll(arr, p)
		// Select(f).Where(p).ToArray() → Array.FindAll(arr, x => p(f(x)))
		if (IsLinqMethodChain(source, nameof(Enumerable.Where), out var whereInvocation)
		    && GetMethodArguments(whereInvocation).FirstOrDefault() is { Expression: { } predicateArg }
		    && TryGetLambda(predicateArg, out var wherePredicate)
		    && TryGetLinqSource(whereInvocation, out var whereSource))
		{
			TryGetOptimizedChainExpression(whereSource, MaterializingMethods, out whereSource);

			// Select(f).Where(p).ToArray() → Array.FindAll(arr, x => p(f(x)))
			if (IsLinqMethodChain(whereSource, nameof(Enumerable.Select), out var selectInvocation)
			    && GetMethodArguments(selectInvocation).FirstOrDefault() is { Expression: { } selectorArg }
			    && TryGetLambda(selectorArg, out var selector)
			    && TryGetLinqSource(selectInvocation, out var selectSource))
			{
				TryGetOptimizedChainExpression(selectSource, MaterializingMethods, out selectSource);

				if (IsInvokedOnArray(context, selectSource))
				{
					var fusedLambda = CombineLambdas(wherePredicate, selector);
					var visitedFused = context.Visit(fusedLambda) as LambdaExpressionSyntax ?? fusedLambda;
					result = CreateInvocation(
						ParseTypeName(nameof(Array)),
						nameof(Array.FindAll),
						selectSource,
						visitedFused);
					return true;
				}
			}

			// Where(p).ToArray() → Array.FindAll(arr, p)
			if (IsInvokedOnArray(context, whereSource))
			{
				var visitedPredicate = context.Visit(wherePredicate) as LambdaExpressionSyntax ?? wherePredicate;
				
				result = CreateInvocation(
					ParseTypeName(nameof(Array)),
					nameof(Array.FindAll),
					whereSource,
					visitedPredicate);
				return true;
			}
		}

		// Skip all materializing/type-cast operations
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}

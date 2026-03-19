using System;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ElementAt context.Method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().ElementAt(index) => collection.ElementAt(index) (type cast doesn't affect indexing)
/// - collection.ToList().ElementAt(index) => collection.ElementAt(index) (materialization doesn't affect indexing)
/// - collection.ToArray().ElementAt(index) => collection.ElementAt(index) (materialization doesn't affect indexing)
/// - array.ElementAt(index) => array[index] (direct array access is faster)
/// - list.ElementAt(index) => list[index] (direct list indexing is faster)
/// - collection.ElementAt(0) => collection.First() (semantically equivalent, more idiomatic)
/// - collection.Skip(n).ElementAt(m) => collection.ElementAt(n + m) => collection[n + m] (index adjustment for Skip)
/// Note: OrderBy/OrderByDescending/Reverse DOES affect element positions, so we don't optimize those!
/// Note: Distinct/Where/Select change the collection, so we don't optimize those either!
/// </summary>
public class ElementAtFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ElementAt), 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source)
		    || context.VisitedParameters.Count == 0)
		{
			result = null;
			return false;
		}

		var indexParameter = context.VisitedParameters[0];

		// Recursively skip all operations that don't affect indexing
		var isNewSource = TryGetOptimizedChainExpression(source, MaterializingMethods, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		var type = context.Method.ReturnType;
		var intType = context.Model.Compilation.CreateInt32();

		while (IsLinqMethodChain(context.Visit(source) ?? source, nameof(Enumerable.Skip), out var skipInvocation)
		       && TryGetLinqSource(skipInvocation, out source)
		       && GetMethodArguments(skipInvocation).FirstOrDefault() is { Expression: { } skipCount })
		{
			indexParameter = OptimizeArithmetic(context, SyntaxKind.AddExpression, indexParameter, skipCount, intType);
			isNewSource = true;

			TryGetOptimizedChainExpression(source, MaterializingMethods, out source);
		}

		if (TryExecutePredicates(context, source, [ indexParameter ], out result))
		{
			return true;
		}

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.Range) when invocation.ArgumentList.Arguments is [ var startArg, var countArg ]:
				{
					// Range(start, count).ElementAt(n) => start + n (if n < count) else throw exception
					result = ConditionalExpression(
						OptimizeComparison(context, SyntaxKind.LessThanExpression, indexParameter, countArg.Expression, intType),
						OptimizeArithmetic(context, SyntaxKind.AddExpression, startArg.Expression, indexParameter, intType),
						CreateThrowExpression<ArgumentOutOfRangeException>());
					return true;
				}
				case nameof(Enumerable.Repeat) when invocation.ArgumentList.Arguments is [ var repeatElementArg, var repeatCountArg ]:
				{
					// Repeat(element, count).ElementAt(n) => n < count ? element : throw exception
					result = ConditionalExpression(
						OptimizeComparison(context, SyntaxKind.LessThanExpression, indexParameter, repeatCountArg.Expression, intType),
						repeatElementArg.Expression,
						CreateThrowExpression<ArgumentOutOfRangeException>());
					return true;
				}
			}
		}

		// For arrays, use direct array indexing: arr[index]
		if (IsInvokedOnArray(context, source))
		{
			result = CreateElementAccess(source, indexParameter);
			return true;
		}

		// For List<T>, use direct indexing: list[index]
		if (IsInvokedOnList(context, source))
		{
			result = CreateElementAccess(source, indexParameter);
			return true;
		}

		if (indexParameter is LiteralExpressionSyntax { Token.Value: 0 })
		{
			result = TryOptimizeByOptimizer<FirstFunctionOptimizer>(context, CreateSimpleInvocation(source, nameof(Enumerable.First)));
			return true;
		}

		// If we skipped any operations, create optimized ElementAt() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source, indexParameter);
			return true;
		}

		result = null;
		return false;
	}
}
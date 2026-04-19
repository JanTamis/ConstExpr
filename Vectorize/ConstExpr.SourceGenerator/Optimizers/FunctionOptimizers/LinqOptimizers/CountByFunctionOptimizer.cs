using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.CountBy method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().CountBy(keySelector) => collection.CountBy(keySelector)
/// - collection.ToList().CountBy(keySelector) => collection.CountBy(keySelector)
/// - collection.ToArray().CountBy(keySelector) => collection.CountBy(keySelector)
/// - collection.OrderBy(...).CountBy(keySelector) => collection.CountBy(keySelector)
/// - Enumerable.Empty&lt;T&gt;().CountBy(keySelector) => Enumerable.Empty&lt;KeyValuePair&lt;TKey, int&gt;&gt;()
/// - collection.CountBy(keySelector, null) => collection.CountBy(keySelector)
/// - collection.Where(v => true).CountBy(keySelector) => collection.CountBy(keySelector)
/// - collection.Where(v => false).CountBy(keySelector) => Enumerable.Empty&lt;KeyValuePair&lt;TKey, int&gt;&gt;()
/// </summary>
public class CountByFunctionOptimizer() : BaseLinqFunctionOptimizer("CountBy", 1, 2)
{
	// Ordering doesn't affect which keys appear or how many times they're counted.
	private static readonly HashSet<string> OperationsThatDontAffectCountBy =
	[
		..MaterializingMethods,
		..OrderingOperations,
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectCountBy, out source);

		if (TryExecutePredicates(context, source, out result, out var currentSource))
		{
			return true;
		}

		// Enumerable.Empty<T>().CountBy(keySelector) => Enumerable.Empty<KeyValuePair<TKey, int>>()
		if (IsEmptyEnumerable(currentSource)
		    && context.Method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } returnType)
		{
			result = CreateEmptyEnumerableCall(returnType.TypeArguments[0]);
			return true;
		}

		// Null comparer removal: CountBy(keySelector, null) => CountBy(keySelector)
		if (context.VisitedParameters is [ _, LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression } ])
		{
			result = UpdateInvocation(context, currentSource, context.VisitedParameters.Take(1));
			return true;
		}

		// Chain walk for literal-predicate Where folding
		if (IsLinqMethodChain(currentSource, out var methodName, out var chainInvocation)
		    && TryGetLinqSource(chainInvocation, out var chainSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.Where)
					when GetMethodArguments(chainInvocation).FirstOrDefault() is { Expression: { } predicateArg }
					     && TryGetLambda(predicateArg, out var wherePredicate)
					     && IsLiteralBooleanLambda(wherePredicate, out var literalBool):
				{
					switch (literalBool)
					{
						case true:
						{
							// x.Where(v => true).CountBy(...) => x.CountBy(...)
							TryGetOptimizedChainExpression(chainSource, OperationsThatDontAffectCountBy, out chainSource);
							result = UpdateInvocation(context, chainSource);
							return true;
						}

						case false when context.Method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } emptyType:
						{
							// x.Where(v => false).CountBy(...) => Enumerable.Empty<KeyValuePair<TKey, int>>()
							result = CreateEmptyEnumerableCall(emptyType.TypeArguments[0]);
							return true;
						}
					}

					break;
				}
			}
		}

		if (isNewSource || !AreEquivalent(source, currentSource))
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}
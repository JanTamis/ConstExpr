using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.ToDictionary method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().ToDictionary(keySelector) => collection.ToDictionary(keySelector)
/// - collection.ToList().ToDictionary(keySelector) => collection.ToDictionary(keySelector)
/// - collection.ToArray().ToDictionary(keySelector) => collection.ToDictionary(keySelector)
/// - collection.OrderBy(...).ToDictionary(keySelector) => collection.ToDictionary(keySelector)
/// - collection.OrderByDescending(...).ToDictionary(keySelector) => collection.ToDictionary(keySelector)
/// - collection.Reverse().ToDictionary(keySelector) => collection.ToDictionary(keySelector)
/// - collection.Distinct().ToDictionary(keySelector) => collection.ToDictionary(keySelector)
/// - collection.Select(x => x).ToDictionary(keySelector) => collection.ToDictionary(keySelector)
/// - collection.ToDictionary(keySelector, x => x) => collection.ToDictionary(keySelector) (identity element-selector)
/// - Enumerable.Empty&lt;T&gt;().ToDictionary(keySelector) => new Dictionary&lt;TKey, TValue&gt;()
/// - Enumerable.Empty&lt;T&gt;().ToDictionary(keySelector, elementSelector) => new Dictionary&lt;TKey, TValue&gt;()
/// - collection.Select(selector).ToDictionary(keySelector) => collection.ToDictionary(x => keySelector(selector(x)), selector) (fold Select into ToDictionary)
/// - collection.Where(p1).Where(p2).ToDictionary(keySelector) => collection.Where(p1 &amp;&amp; p2).ToDictionary(keySelector) (merge chained Where predicates)
/// - collection.DistinctBy(selector).ToDictionary(keySelector) => collection.ToDictionary(keySelector) (when keySelector matches selector, DistinctBy is redundant since dictionary keys are unique)
/// </summary>
public class ToDictionaryFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.ToDictionary), n => n is 1 or 2 or 3)
{
	// Ordering and deduplication operations that don't affect the content of the resulting dictionary
	// (Dictionary keys are already unique; ordering has no effect on an unordered collection)
	private static readonly HashSet<string> OperationsThatDontAffectDictionary =
	[
		..MaterializingMethods,
		..OrderingOperations,
		nameof(Enumerable.Distinct)
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectDictionary, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Optimize Enumerable.Empty<T>().ToDictionary(...) => new Dictionary<TKey, TValue>()
		if (IsEmptyEnumerable(source)
		    && context.Method.ReturnType is INamedTypeSymbol { TypeArguments.Length: 2 } returnType)
		{
			result = CreateEmptyDictionaryCreation(returnType.TypeArguments[0], returnType.TypeArguments[1]);
			return true;
		}

		// Optimize ToDictionary(keySelector, x => x) => ToDictionary(keySelector) when element-selector is identity
		if (context.VisitedParameters.Count == 2
		    && TryGetLambda(context.VisitedParameters[1], out var elementSelectorLambda)
		    && IsIdentityLambda(elementSelectorLambda))
		{
			result = CreateInvocation(source, nameof(Enumerable.ToDictionary), context.VisitedParameters[0]);
			return true;
		}

		// Walk the chain for Select / Where / DistinctBy optimizations
		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				// Optimize Select(selector).ToDictionary(keySelector) =>
				//   ToDictionary(x => keySelector(selector(x)), selector)
				// When ToDictionary has only a keySelector (1 param), fold the Select projection
				// into both the keySelector and a new elementSelector.
				case nameof(Enumerable.Select)
					when context.VisitedParameters.Count == 1
					     && GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } selectorArg }
					     && TryGetLambda(selectorArg, out var selectLambda)
					     && TryGetLambda(context.VisitedParameters[0], out var keyLambda):
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectDictionary, out invocationSource);

					// Compose keySelector ∘ selectLambda
					var composedKey = CombineLambdas(keyLambda, selectLambda);

					// Select(selector).ToDictionary(keySelector) => ToDictionary(composedKey, selector)
					result = UpdateInvocation(context, invocationSource, composedKey, selectorArg);
					return true;
				}

				// Optimize Where(predicate).ToDictionary(keySelector) =>
				//   Merge chained Where predicates: Where(p1).Where(p2).ToDictionary(k) => Where(p1 && p2).ToDictionary(k)
				case nameof(Enumerable.Where)
					when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } predicateArg }
					     && TryGetLambda(predicateArg, out var wherePredicate):
				{
					// var whereSource = invocationSource;

					// If the predicate is always true, skip the Where entirely
					if (IsLiteralBooleanLambda(wherePredicate, out var literalValue))
					{
						switch (literalValue)
						{
							case false when context.Method.ReturnType is INamedTypeSymbol { TypeArguments.Length: 2 } emptyReturnType:
							{
								result = CreateEmptyDictionaryCreation(emptyReturnType.TypeArguments[0], emptyReturnType.TypeArguments[1]);
								return true;
							}
						}
					}

					break;
				}

				// Optimize DistinctBy(selector).ToDictionary(keySelector) => ToDictionary(keySelector)
				// When the DistinctBy selector matches the keySelector, the DistinctBy is redundant
				// because dictionary keys are already unique.
				case "DistinctBy"
					when context.VisitedParameters.Count >= 1
					     && GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } distinctByArg }
					     && TryGetLambda(distinctByArg, out var distinctByLambda)
					     && TryGetLambda(context.VisitedParameters[0], out var dictKeyLambda)
					     && TryGetLambdaBody(distinctByLambda, out var distinctByBody)
					     && TryGetLambdaBody(dictKeyLambda, out var dictKeyBody)
					     && AreSyntacticallyEquivalent(
						     ReplaceIdentifier(distinctByBody, GetLambdaParameter(distinctByLambda), IdentifierName("__x")),
						     ReplaceIdentifier(dictKeyBody, GetLambdaParameter(dictKeyLambda), IdentifierName("__x"))):
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectDictionary, out invocationSource);
					result = UpdateInvocation(context, invocationSource);
					return true;
				}
			}
		}

		// Strip redundant operations (e.g. .ToList().ToDictionary() => .ToDictionary())
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}

	private static ObjectCreationExpressionSyntax CreateEmptyDictionaryCreation(ITypeSymbol keyType, ITypeSymbol valueType)
	{
		return ObjectCreationExpression(
				GenericName(Identifier("Dictionary"))
					.WithTypeArgumentList(
						TypeArgumentList(
							SeparatedList<TypeSyntax>(
							[
								ParseTypeName(keyType.ToString()),
								ParseTypeName(valueType.ToString())
							]))))
			.WithArgumentList(ArgumentList());
	}
}
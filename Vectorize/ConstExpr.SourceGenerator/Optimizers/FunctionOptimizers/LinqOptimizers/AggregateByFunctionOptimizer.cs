using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.AggregateBy method.
/// Optimizes patterns such as:
/// - collection.AsEnumerable().AggregateBy(keySelector, seed, func) => collection.AggregateBy(keySelector, seed, func)
/// - collection.ToList().AggregateBy(keySelector, seed, func) => collection.AggregateBy(keySelector, seed, func)
/// - collection.ToArray().AggregateBy(keySelector, seed, func) => collection.AggregateBy(keySelector, seed, func)
/// - Enumerable.Empty&lt;T&gt;().AggregateBy(keySelector, seed, func) => Enumerable.Empty&lt;KeyValuePair&lt;TKey, TAccumulate&gt;&gt;()
/// - collection.AggregateBy(keySelector, seed, func, null) => collection.AggregateBy(keySelector, seed, func)
/// - collection.AggregateBy(keySelector, 0, (acc, _) => acc + 1) => collection.CountBy(keySelector)
/// - collection.Select(v => v).AggregateBy(keySelector, seed, func) => collection.AggregateBy(keySelector, seed, func)
/// - collection.Select(selector).AggregateBy(keySelector, seed, func) => collection.AggregateBy(v => keySelector(selector(v)), seed, (acc, v) => func(acc, selector(v)))
/// - collection.Where(v => true).AggregateBy(...) => collection.AggregateBy(...)
/// - collection.Where(v => false).AggregateBy(...) => Enumerable.Empty&lt;KeyValuePair&lt;TKey, TAccumulate&gt;&gt;()
/// Note: Ordering operations are NOT stripped because the accumulator function is applied to elements
/// in the order they appear within each key group, so ordering can affect non-commutative accumulators.
/// </summary>
public class AggregateByFunctionOptimizer() : BaseLinqFunctionOptimizer("AggregateBy", n => n is 3 or 4 or 5)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		var isNewSource = TryGetOptimizedChainExpression(source, MaterializingMethods, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Enumerable.Empty<T>().AggregateBy(...) => Enumerable.Empty<KeyValuePair<TKey, TAccumulate>>()
		if (IsEmptyEnumerable(source)
		    && context.Method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } returnType)
		{
			result = CreateEmptyEnumerableCall(returnType.TypeArguments[0]);
			return true;
		}

		// CountBy pattern: AggregateBy(keySelector, 0, (acc, _) => acc + 1) => CountBy(keySelector)
		if (TryOptimizeToCountBy(context, source, out result))
		{
			return true;
		}

		// Null comparer removal: AggregateBy(k, s, f, null) => AggregateBy(k, s, f)
		if (context.VisitedParameters is [ _, _, _, LiteralExpressionSyntax { RawKind: (int) SyntaxKind.NullLiteralExpression } ])
		{
			result = UpdateInvocation(context, source, context.VisitedParameters.Take(3));
			return true;
		}

		// Chain walk for Select and Where
		if (IsLinqMethodChain(source, out var methodName, out var chainInvocation)
		    && TryGetLinqSource(chainInvocation, out var chainSource))
		{
			switch (methodName)
			{
				// Select folding: Select(selector).AggregateBy(k, s, f) =>
				//   AggregateBy(v => k(selector(v)), s, (acc, v) => f(acc, selector(v)))
				case nameof(Enumerable.Select)
					when context.VisitedParameters.Count >= 3
					     && GetMethodArguments(chainInvocation).FirstOrDefault() is { Expression: { } selectorArg }
					     && TryGetLambda(selectorArg, out var selectLambda)
					     && TryGetLambda(context.VisitedParameters[0], out var keyLambda)
					     && TryGetLambda(context.VisitedParameters[2], out var funcLambda)
					     && funcLambda is ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 2 } pFuncLambda:
				{
					TryGetOptimizedChainExpression(chainSource, MaterializingMethods, out chainSource);

					if (IsIdentityLambda(selectLambda))
					{
						// x.Select(v => v).AggregateBy(k, s, f) => x.AggregateBy(k, s, f)
						result = UpdateInvocation(context, chainSource);
						return true;
					}

					// Compose keySelector ∘ selectLambda: v => keySelector(selector(v))
					var composedKey = CombineLambdas(keyLambda, selectLambda);

					// Fold selectLambda into the func's element parameter:
					// (acc, elem) => body  +  s => selectorBody  =>  (acc, s) => body.Replace(elem, selectorBody)
					var accParam = pFuncLambda.ParameterList.Parameters[0].Identifier.Text;
					var elemParam = pFuncLambda.ParameterList.Parameters[1].Identifier.Text;
					var selectParam = GetLambdaParameter(selectLambda);

					// Avoid duplicate parameter names (e.g. if accParam == selectParam)
					if (accParam == selectParam)
					{
						break;
					}

					var foldedFuncBody = ReplaceIdentifier(GetLambdaBody(pFuncLambda), elemParam, GetLambdaBody(selectLambda));
					var foldedFunc = ParenthesizedLambdaExpression(
						ParameterList(SeparatedList<ParameterSyntax>(
						[
							Parameter(Identifier(accParam)),
							Parameter(Identifier(selectParam)),
						])),
						foldedFuncBody);

					// Pass comparer through if it was present
					ExpressionSyntax[] newArgs = context.VisitedParameters.Count == 4
						? [ composedKey, context.VisitedParameters[1], foldedFunc, context.VisitedParameters[3] ]
						: [ composedKey, context.VisitedParameters[1], foldedFunc ];

					result = UpdateInvocation(context, chainSource, newArgs);
					return true;
				}

				// Literal-predicate Where folding
				case nameof(Enumerable.Where)
					when GetMethodArguments(chainInvocation).FirstOrDefault() is { Expression: { } predicateArg }
					     && TryGetLambda(predicateArg, out var wherePredicate)
					     && IsLiteralBooleanLambda(wherePredicate, out var literalBool):
				{
					switch (literalBool)
					{
						case true:
							// x.Where(v => true).AggregateBy(...) => x.AggregateBy(...)
							TryGetOptimizedChainExpression(chainSource, MaterializingMethods, out chainSource);
							
							result = UpdateInvocation(context, chainSource);
							return true;

						case false when context.Method.ReturnType is INamedTypeSymbol { TypeArguments.Length: > 0 } emptyType:
							// x.Where(v => false).AggregateBy(...) => Enumerable.Empty<KeyValuePair<TKey, TAccumulate>>()
							result = CreateEmptyEnumerableCall(emptyType.TypeArguments[0]);
							return true;
					}

					break;
				}
			}
		}

		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}

	/// <summary>
	/// Detects AggregateBy(keySelector, 0, (acc, _) => acc + 1[, comparer]) and converts to
	/// CountBy(keySelector[, comparer]), because both produce identical
	/// IEnumerable&lt;KeyValuePair&lt;TKey, int&gt;&gt; results counting occurrences per key.
	/// </summary>
	private bool TryOptimizeToCountBy(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		if (context.VisitedParameters.Count < 3)
		{
			return false;
		}

		// Seed must be integer zero
		if (context.VisitedParameters[1] is not LiteralExpressionSyntax { Token.Value: 0 })
		{
			return false;
		}

		// Func must be a two-parameter lambda
		if (!TryGetLambda(context.VisitedParameters[2], out var funcLambda)
		    || funcLambda is not ParenthesizedLambdaExpressionSyntax { ParameterList.Parameters.Count: 2 } pFuncLambda
		    || !TryGetLambdaBody(funcLambda, out var funcBody))
		{
			return false;
		}

		var accParam = pFuncLambda.ParameterList.Parameters[0].Identifier.Text;
		var elemParam = pFuncLambda.ParameterList.Parameters[1].Identifier.Text;

		// Body must be acc + 1 (or 1 + acc) and the element parameter must not appear in the body
		if (!IsIncrementBody(funcBody, accParam) 
		    || funcBody.HasIdentifier(elemParam))
		{
			return false;
		}

		// Delegate to CountByFunctionOptimizer so CountBy-level optimizations are also applied.
		// AggregateBy has type args [TSource, TKey, TAccumulate]; CountBy only needs [TSource, TKey].
		var countByInvocation = context.VisitedParameters.Count == 4
			? CreateInvocation(source, "CountBy", context.VisitedParameters[0], context.VisitedParameters[3])
			: CreateInvocation(source, "CountBy", context.VisitedParameters[0]);

		result = TryOptimizeByOptimizer<CountByFunctionOptimizer>(
			context,
			countByInvocation,
			context.Method.TypeArguments.Take(2).ToArray());

		return true;
	}

	/// <summary>
	/// Returns true when <paramref name="body"/> is <c>accParam + 1</c> or <c>1 + accParam</c>.
	/// </summary>
	private static bool IsIncrementBody(ExpressionSyntax body, string accParam)
	{
		if (body is not BinaryExpressionSyntax { RawKind: (int) SyntaxKind.AddExpression } add)
		{
			return false;
		}

		var leftIsAcc = add.Left is IdentifierNameSyntax leftId && leftId.Identifier.Text == accParam;
		var rightIsOne = add.Right is LiteralExpressionSyntax { Token.Value: 1 };

		if (leftIsAcc && rightIsOne)
		{
			return true;
		}

		var leftIsOne = add.Left is LiteralExpressionSyntax { Token.Value: 1 };
		var rightIsAcc = add.Right is IdentifierNameSyntax rightId && rightId.Identifier.Text == accParam;

		return leftIsOne && rightIsAcc;
	}
}
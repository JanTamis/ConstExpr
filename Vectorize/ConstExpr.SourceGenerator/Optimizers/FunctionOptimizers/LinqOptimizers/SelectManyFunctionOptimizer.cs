using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SelectMany context.Method.
/// Optimizes patterns such as:
/// - collection.SelectMany(x => Enumerable.Empty&lt;T&gt;()) => Enumerable.Empty&lt;T&gt;()
/// - collection.SelectMany(x => new[] { x }) => collection (identity flattening)
/// - Enumerable.Empty&lt;T&gt;().SelectMany(selector) => Enumerable.Empty&lt;TResult&gt;()
/// </summary>
public class SelectManyFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.SelectMany), 1, 2)
{
	public override bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Optimize Enumerable.Empty<T>().SelectMany(selector) => Enumerable.Empty<TResult>()
		if (IsEmptyEnumerable(source)
		    && context.Method.TypeArguments.Length > 0)
		{
			// Get the result type (last type argument)
			var resultType = context.Method.TypeArguments[^1];
			result = CreateEmptyEnumerableCall(resultType);
			return true;
		}

		// Check if lambda always returns empty
		if (context.VisitedParameters.Count >= 1
		    && TryGetLambda(context.VisitedParameters[0], out var lambda)
		    && TryGetLambdaBody(lambda, out var body)
		    && IsEmptyEnumerable(body)
		    && context.Method.TypeArguments.Length > 0)
		{
			if (IsEmptyEnumerable(body))
			{
				// selector always returns empty, so result is empty
				var resultType = context.Method.TypeArguments[^1];

				result = CreateEmptyEnumerableCall(resultType);
				return true;
			}

			if (IsLinqMethodChain(source, out var methodName, out var invocation)
			    && TryGetLinqSource(invocation, out var invocationSource))
			{
				switch (methodName)
				{
					case nameof(Enumerable.Select) when context.VisitedParameters.Count == 1
					                                    && TryGetLambda(context.VisitedParameters[0], out var selectLambda)
					                                    && IsIdentityLambda(selectLambda):
					{
						result = UpdateInvocation(context, invocationSource, CombineLambdas(lambda, selectLambda));
						return true;
					}
				}
			}
		}

		result = null;
		return false;
	}
}
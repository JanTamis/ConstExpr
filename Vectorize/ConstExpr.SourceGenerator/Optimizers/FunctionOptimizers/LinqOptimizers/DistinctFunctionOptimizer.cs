using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Distinct context.Method.
/// Optimizes patterns such as:
/// - collection.Distinct().Distinct() => collection.Distinct() (redundant Distinct calls)
/// - collection.Select(x => x).Distinct() => collection.Distinct() (identity Select before Distinct)
/// - collection.AsEnumerable().Distinct() => collection.Distinct() (type cast doesn't affect distinctness)
/// - collection.ToList().Distinct() => collection.Distinct() (materialization doesn't affect distinctness)
/// - collection.ToArray().Distinct() => collection.Distinct() (materialization doesn't affect distinctness)
/// - collection.OrderBy(...).Distinct().Count() => collection.Distinct().Count() (when followed by set-based operations)
/// Note: OrderBy/Reverse DOES affect the ORDER of distinct results, so we only optimize when followed by
///       operations that don't care about order (Count, Any, Contains, etc.)
/// </summary>
public class DistinctFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Distinct), 0)
{
	// Operations that don't affect the result of Distinct (both values AND order)
	// We CANNOT include ordering operations because they change the ORDER of distinct results!
	private static readonly HashSet<string> OperationsThatDontAffectDistinctness =
	[
		..MaterializingMethods,
		nameof(Enumerable.Distinct),         // Redundant Distinct calls
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
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

		// Check if Distinct is followed by a set-based operation
		var parent = context.Invocation.Parent;
		var isFollowedBySetOperation = false;
		
		if (parent is MemberAccessExpressionSyntax { Parent: InvocationExpressionSyntax parentInvocation } memberAccess
		    && parentInvocation.Expression == memberAccess)
		{
			isFollowedBySetOperation = SetBasedOperations.Contains(memberAccess.Name.Identifier.Text);
		}

		// Determine which operations can be skipped
		var allowedOperations = isFollowedBySetOperation
			? new HashSet<string>(OperationsThatDontAffectDistinctness.Union(OrderingOperations))
			: OperationsThatDontAffectDistinctness;

		// Recursively skip all allowed operations
		var isNewSource = TryGetOptimizedChainExpression(source, allowedOperations, out source);

		if (TryExecutePredicates(context, source, out result, out _))
		{
			return true;
		}

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case var name when SetBasedOperations.Contains(name):
				{
					result = context.Visit(invocationSource) ?? invocationSource;
					return true;
				}
				// Check for identity Select
				case nameof(Enumerable.Select) when GetMethodArguments(invocation).FirstOrDefault() is { Expression: { } lambdaArg }
				                                    && TryGetLambda(lambdaArg, out var lambda)
				                                    && IsIdentityLambda(context.Visit(lambda) as LambdaExpressionSyntax ?? lambda):
				{
					TryGetOptimizedChainExpression(invocationSource, allowedOperations, out invocationSource);

					if (TryExecutePredicates(context, invocationSource, out result, out _))
					{
						return true;
					}

					result = UpdateInvocation(context, invocationSource);
					return true;
				}
			}
		}

		// If we skipped any operations, create optimized Distinct() call
		if (isNewSource)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}

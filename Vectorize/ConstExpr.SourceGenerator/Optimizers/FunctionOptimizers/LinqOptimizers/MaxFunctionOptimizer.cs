using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MathMaxOptimizer = ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers.MaxFunctionOptimizer;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Max context.Method.
/// Optimizes patterns such as:
/// - collection.Max(x => x) => collection.Max() (identity lambda removal)
/// - collection.Select(x => x.Property).Max() => collection.Max(x => x.Property)
/// - collection.OrderBy(...).Max() => collection.Max() (ordering doesn't affect max)
/// - collection.AsEnumerable().Max() => collection.Max()
/// - collection.ToList().Max() => collection.Max()
/// </summary>
public class MaxFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Max), 0, 1)
{
	// Operations that don't affect the maximum value
	private static readonly HashSet<string> OperationsThatDontAffectMax =
	[
		..MaterializingMethods,
		..OrderingOperations,
	];

	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}
		
		// Recursively skip operations that don't affect max
		var isNewSource = TryGetOptimizedChainExpression(source, OperationsThatDontAffectMax, out source);

		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}
		
		// Optimize Max(x => x) => Max() (identity lambda removal)
		if (context.VisitedParameters.Count == 1
		    && TryGetLambda(context.VisitedParameters[0], out var lambda)
		    && IsIdentityLambda(lambda))
		{
			result = UpdateInvocation(context, source, []);
			return true;
		}

		if (context.VisitedParameters.Count == 0
		    && IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				// Optimize source.Select(selector).Max() => source.Max(selector)
				case nameof(Enumerable.Select) when invocation.ArgumentList.Arguments.Count == 1:
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectMax, out invocationSource);

					var selector = invocation.ArgumentList.Arguments[0].Expression;

					result = UpdateInvocation(context, invocationSource, selector);
					return true;
				}
				case nameof(Enumerable.Concat):
				{
					TryGetOptimizedChainExpression(invocationSource, OperationsThatDontAffectMax, out invocationSource);

					var left = TryOptimize(context.WithInvocationAndMethod(UpdateInvocation(context, invocationSource), context.Method), out var leftResult) ? leftResult as ExpressionSyntax : null;
					var right = TryOptimize(context.WithInvocationAndMethod(CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters), context.Method), out var rightResult) ? rightResult as ExpressionSyntax : null;

					var leftExpr = left ?? CreateInvocation(invocationSource, Name, context.VisitedParameters);
					var rightExpr = right ?? CreateInvocation(invocation.ArgumentList.Arguments[0].Expression, Name, context.VisitedParameters);

					result = OptimizeAsMathPairwise<MathMaxOptimizer>(context, leftExpr, rightExpr);
					return true;
				}
			}
		}

		// If we skipped any operations, create optimized Max() call
		if (isNewSource && context.VisitedParameters.Count == 0)
		{
			result = UpdateInvocation(context, source);
			return true;
		}

		result = null;
		return false;
	}
}

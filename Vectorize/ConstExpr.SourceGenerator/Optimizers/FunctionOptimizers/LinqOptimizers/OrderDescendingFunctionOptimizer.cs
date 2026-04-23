using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.OrderDescending context.Method.
/// Optimizes patterns such as:
/// - collection.OrderDescending().OrderDescending() => collection.OrderDescending() (redundant order)
/// </summary>
public class OrderDescendingFunctionOptimizer() : BaseLinqFunctionOptimizer("OrderDescending", n => n is 0)
{
	private static readonly HashSet<string> OrderingOperations =
	[
		..MaterializingMethods,
		nameof(Enumerable.OrderBy),
		nameof(Enumerable.OrderByDescending),
		"Order",
		"OrderDescending"
	];

	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		var isNewSource = TryGetOptimizedChainExpression(source, OrderingOperations, out source);

		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case "OrderDescending":
				{
					result = invocationSource;
					return true;
				}
				case "Order":
				{
					result = UpdateInvocation(context, invocationSource);
					return true;
				}
			}
		}

		if (isNewSource)
		{
			result = CreateSimpleInvocation(source, Name);
			return true;
		}

		result = null;
		return false;
	}
}
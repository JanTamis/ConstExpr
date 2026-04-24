using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Reverse context.Method.
/// Optimizes patterns such as:
/// - collection.Reverse().Reverse() => collection (double reverse cancels out)
/// </summary>
public class ReverseFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Reverse), n => n is 0)
{
	protected override bool TryOptimizeLinq(FunctionOptimizerContext context, ExpressionSyntax source, [NotNullWhen(true)] out SyntaxNode? result)
	{
		if (TryExecutePredicates(context, source, out result, out source))
		{
			return true;
		}

		// Optimize Reverse().Reverse() => original collection (double reverse cancels out)
		if (IsLinqMethodChain(source, out var methodName, out var invocation)
		    && TryGetLinqSource(invocation, out var invocationSource))
		{
			switch (methodName)
			{
				case nameof(Enumerable.Reverse):
				{
					result = invocationSource;
					return true;
				}
				case "Order":
				{
					result = CreateInvocation(invocationSource, "OrderDescending");
					return true;
				}
				case nameof(Enumerable.OrderBy):
				{
					result = CreateInvocation(invocationSource, nameof(Enumerable.OrderByDescending), invocation.ArgumentList.Arguments.Select(s => s.Expression));
					return true;
				}
				case "OrderDescending":
				{
					result = CreateInvocation(invocationSource, "Order");
					return true;
				}
				case nameof(Enumerable.OrderByDescending):
				{
					result = CreateInvocation(invocationSource, nameof(Enumerable.OrderBy), invocation.ArgumentList.Arguments.Select(s => s.Expression));
					return true;
				}
			}
		}

		result = null;
		return false;
	}
}
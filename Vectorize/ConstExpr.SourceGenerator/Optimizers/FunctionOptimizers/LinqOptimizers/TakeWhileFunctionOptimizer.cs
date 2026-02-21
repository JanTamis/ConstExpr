using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.TakeWhile context.Method.
/// Optimizes patterns such as:
/// - collection.TakeWhile(x => true) => collection (take everything)
/// - collection.TakeWhile(x => false) => Enumerable.Empty&lt;T&gt;() (take nothing)
/// </summary>
public class TakeWhileFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.TakeWhile), 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLambda(context.VisitedParameters[0], out var lambda)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		if (IsLiteralBooleanLambda(lambda, out var value))
		{
			switch (value)
			{
				// Optimize TakeWhile(x => true) => collection (take everything)
				case true:
					result = context.Visit(source) ?? source;
					return true;
				// Optimize TakeWhile(x => false) => Enumerable.Empty<T>() (take nothing)
				case false:
					result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
					return true;
			}
		}

		result = null;
		return false;
	}
}
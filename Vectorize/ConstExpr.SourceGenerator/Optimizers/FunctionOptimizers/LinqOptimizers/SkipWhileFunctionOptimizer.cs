using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.SkipWhile context.Method.
/// Optimizes patterns such as:
/// - collection.SkipWhile(x => false) => collection (skip nothing)
/// - collection.SkipWhile(x => true) => Enumerable.Empty&lt;T&gt;() (skip everything)
/// </summary>
public class SkipWhileFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.SkipWhile), 1)
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
				case false:
					result = context.Visit(source) ?? source;
					return true;
				case true:
					result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
					return true;
			}
		}

		result = null;
		return false;
	}
}


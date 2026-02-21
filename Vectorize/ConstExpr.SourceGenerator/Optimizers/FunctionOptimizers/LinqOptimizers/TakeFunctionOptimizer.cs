using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Take context.Method.
/// Optimizes patterns such as:
/// - collection.Take(0) =&gt; Enumerable.Empty&lt;T&gt;() (replace with empty collection)
/// - collection.Skip(n).Take(m) =&gt; potential range optimization
/// </summary>
public class TakeFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Take), 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || !TryGetLinqSource(context.Invocation, out var source))
		{
			result = null;
			return false;
		}

		if (TryExecutePredicates(context, source, out result))
		{
			return true;
		}

		if (context.VisitedParameters[0] is not LiteralExpressionSyntax { Token.Value: <= 0 })
		{
			result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}
}

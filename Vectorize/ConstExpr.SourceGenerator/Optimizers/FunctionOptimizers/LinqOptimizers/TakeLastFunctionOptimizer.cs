using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.TakeLast context.Method.
/// Optimizes patterns such as:
/// - collection.TakeLast(0) => Enumerable.Empty&lt;T&gt;() (take nothing)
/// </summary>
public class TakeLastFunctionOptimizer() : BaseLinqFunctionOptimizer("TakeLast", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || context.VisitedParameters[0] is not LiteralExpressionSyntax { Token.Value: int count })
		{
			result = null;
			return false;
		}

		// Optimize TakeLast(0) => Enumerable.Empty<T>()
		if (count <= 0)
		{
			result = CreateEmptyEnumerableCall(context.Method.TypeArguments[0]);
			return true;
		}

		result = null;
		return false;
	}
}


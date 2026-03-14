using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

/// <summary>
/// Optimizer for Enumerable.Range(start, count).
/// Optimizes patterns such as:
/// - Enumerable.Range(start, 0) => [] (empty range, regardless of start value)
/// </summary>
public class RangeFunctionOptimizer() : BaseLinqFunctionOptimizer("Range", 2)
{
	public override bool TryOptimize(FunctionOptimizerContext context, [NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;

		if (!IsValidLinqMethod(context))
		{
			return false;
		}

		var startExpr = context.VisitedParameters[0];
		var countExpr = context.VisitedParameters[1];

		// Enumerable.Range(start, 0) => [] (empty, regardless of start)
		if (countExpr is LiteralExpressionSyntax { Token.Value: 0 })
		{
			result = CreateEmptyEnumerableCall(context.Model.Compilation.CreateInt32());
			return true;
		}

		return false;
	}
}




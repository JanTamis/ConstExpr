using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class SkipFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Skip), 1)
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

		if (context.VisitedParameters[0] is LiteralExpressionSyntax { Token.Value: <= 0 })
		{
			result = context.Visit(source) ?? source;
			return true;
		}

		result = null;
		return false;
	}
}
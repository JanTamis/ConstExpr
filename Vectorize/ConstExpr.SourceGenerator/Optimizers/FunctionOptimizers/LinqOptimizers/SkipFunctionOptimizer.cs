using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class SkipFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Skip), 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(context.Model, context.Method)
		    || context.Invocation.Expression is not MemberAccessExpressionSyntax memberAccess
		    || context.VisitedParameters[0] is not LiteralExpressionSyntax { Token.Value: int count }
		    || count > 0)
		{
			result = null;
			return false;
		}
		
		result = context.Visit(memberAccess.Expression) ?? memberAccess.Expression;
		return true;
	}
}
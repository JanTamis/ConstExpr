using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class OrderByFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.OrderBy), 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || !TryGetLambda(parameters[0], out var lambda)
		    || !TryGetLinqSource(invocation, out var source))
		{
			result = null;
			return false;
		}

		// Optimize OrderBy(x => x) => Order() (identity lambda)
		if (IsIdentityLambda(lambda))
		{
			result = CreateSimpleInvocation(visit(source) ?? source, "Order");
			return true;
		}
		
		result = null;
		return false;
	}
}
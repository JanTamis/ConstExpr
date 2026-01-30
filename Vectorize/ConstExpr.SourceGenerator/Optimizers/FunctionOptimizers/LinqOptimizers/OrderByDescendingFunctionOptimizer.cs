using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class OrderByDescendingFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.OrderByDescending), 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(method)
		    || !TryGetLambda(parameters[0], out var lambda)
		    || !IsIdentityLambda(lambda)
		    || invocation.Expression is not MemberAccessExpressionSyntax memberAccess)
		{
			result = null;
			return false;
		}

		result = CreateSimpleLinqMethodCall(memberAccess.Expression, "OrderDescending");
		return true;
	}
}
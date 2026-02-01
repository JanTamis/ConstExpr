using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.LinqOptimizers;

public class SkipFunctionOptimizer() : BaseLinqFunctionOptimizer(nameof(Enumerable.Skip), 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		if (!IsValidLinqMethod(model, method)
		    || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
		    || parameters[0] is not LiteralExpressionSyntax { Token.Value: int count }
		    || count > 0)
		{
			result = null;
			return false;
		}
		
		result = memberAccess.Expression;
		return true;
	}
}
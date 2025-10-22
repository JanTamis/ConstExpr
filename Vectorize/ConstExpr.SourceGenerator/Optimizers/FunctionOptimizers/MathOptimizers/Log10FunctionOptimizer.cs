using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class Log10FunctionOptimizer() : BaseMathFunctionOptimizer("Log10", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(method, out var paramType))
		{
			return false;
		}

		var arg = parameters[0];

		// Log10(Exp10(x)) => x (inverse operation)
		if (arg is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Exp10" }, ArgumentList.Arguments.Count: 1 } inv
		    && IsPure(inv.ArgumentList.Arguments[0].Expression))
		{
			result = inv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		return false;
	}
}
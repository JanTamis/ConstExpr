using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class BitIncrementFunctionOptimizer() : BaseMathFunctionOptimizer("BitIncrement", 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, Func<SyntaxNode, ExpressionSyntax?> visit, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(method, out var paramType))
		{
			return false;
		}

		// BitIncrement(BitDecrement(x)) -> x (inverse operations)
		if (parameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "BitDecrement" }, ArgumentList.Arguments.Count: 1 } innerInv)
		{
			result = innerInv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		// Default: keep as BitIncrement call (target numeric helper type)
		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}
}

using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class BitDecrementFunctionOptimizer() : BaseMathFunctionOptimizer("BitDecrement", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(context.Method, out var paramType))
		{
			return false;
		}

		// BitDecrement(BitIncrement(x)) -> x (inverse operations)
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "BitIncrement" }, ArgumentList.Arguments.Count: 1 } innerInv)
		{
			result = innerInv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		// Default: keep as BitDecrement call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}
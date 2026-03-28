using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class BitIncrementFunctionOptimizer() : BaseMathFunctionOptimizer("BitIncrement", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// BitIncrement(BitDecrement(x)) -> x (inverse operations)
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "BitDecrement" }, ArgumentList.Arguments.Count: 1 } innerInv)
		{
			result = innerInv.ArgumentList.Arguments[0].Expression;
			return true;
		}

		// Default: keep as BitIncrement call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}

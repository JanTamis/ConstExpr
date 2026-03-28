using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CeilingFunctionOptimizer() : BaseMathFunctionOptimizer("Ceiling", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
		// 1) Idempotence: Ceiling(Ceiling(x)) -> Ceiling(x)
		if (context.VisitedParameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Ceiling" } } innerInv)
		{
			result = innerInv;
			return true;
		}

		// 2) Unary minus: Ceiling(-x) -> -Floor(x)
		if (context.VisitedParameters[0] is PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken } prefix)
		{
			var floorCall = CreateInvocation(paramType, "Floor", prefix.Operand);

			result = UnaryMinusExpression(ParenthesizedExpression(floorCall));
			return true;
		}

		// Default: keep as Ceiling call (target numeric helper type)
		result = CreateInvocation(paramType, Name, context.VisitedParameters);
		return true;
	}
}

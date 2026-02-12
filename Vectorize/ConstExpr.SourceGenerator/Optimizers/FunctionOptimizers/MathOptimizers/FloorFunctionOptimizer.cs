using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class FloorFunctionOptimizer() : BaseMathFunctionOptimizer("Floor", 1)
{
	public override bool TryOptimize(FunctionOptimizerContext context, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(context.Method, out var paramType))
		{
			return false;
		}

		var arg = context.VisitedParameters[0];

		switch (arg)
		{
			// 1) Idempotence: Floor(Floor(x)) -> Floor(x)
			case InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Floor" } } innerInv:
			{
				result = innerInv;
				return true;
			}
			// 2) Unary minus: Floor(-x) -> -Ceiling(x)
			case PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken } prefix:
			{
				var ceilingCall = CreateInvocation(paramType, "Ceiling", prefix.Operand);

				result = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, SyntaxFactory.ParenthesizedExpression(ceilingCall));
				return true;
			}
			default:
			{
				// Default: keep as Floor call (target numeric helper type)
				result = CreateInvocation(paramType, Name, context.VisitedParameters);
				return true;
			}
		}

		return false;
	}
}

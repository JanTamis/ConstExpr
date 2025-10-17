using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class FloorFunctionOptimizer() : BaseMathFunctionOptimizer("Floor", 1)
{
	public override bool TryOptimize(IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(method, out var paramType))
		{
			return false;
		}

		switch (parameters[0])
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
				result = CreateInvocation(paramType, Name, parameters);
				return true;
			}
		}

	}
}

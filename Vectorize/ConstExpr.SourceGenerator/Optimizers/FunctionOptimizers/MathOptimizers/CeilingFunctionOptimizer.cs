using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class CeilingFunctionOptimizer() : BaseMathFunctionOptimizer("Ceiling", 1)
{
	public override bool TryOptimize(SemanticModel model, IMethodSymbol method, InvocationExpressionSyntax invocation, IList<ExpressionSyntax> parameters, IDictionary<SyntaxNode, bool> additionalMethods, out SyntaxNode? result)
	{
		result = null;

		if (!IsValidMathMethod(method, out var paramType))
		{
			return false;
		}

		// 1) Idempotence: Ceiling(Ceiling(x)) -> Ceiling(x)
		if (parameters[0] is InvocationExpressionSyntax { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Ceiling" } } innerInv)
		{
			result = innerInv;
			return true;
		}

		// 2) Unary minus: Ceiling(-x) -> -Floor(x)
		if (parameters[0] is PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken } prefix)
		{
			var floorCall = CreateInvocation(paramType, "Floor", prefix.Operand);

			result = SyntaxFactory.PrefixUnaryExpression(SyntaxKind.UnaryMinusExpression, SyntaxFactory.ParenthesizedExpression(floorCall));
			return true;
		}

		// Default: keep as Ceiling call (target numeric helper type)
		result = CreateInvocation(paramType, Name, parameters);
		return true;
	}
}

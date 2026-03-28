using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers;

public class FloorFunctionOptimizer() : BaseMathFunctionOptimizer("Floor", 1)
{
	protected override bool TryOptimizeMath(FunctionOptimizerContext context, ITypeSymbol paramType, [NotNullWhen(true)] out SyntaxNode? result)
	{
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

				result = UnaryMinusExpression(ParenthesizedExpression(ceilingCall));
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

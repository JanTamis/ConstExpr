using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.EqualsStrategies;

/// <summary>
/// Strategy for Count() == 0 → !source.Any() (and 0 == Count()).
/// Any() short-circuits after the first element; Count() must enumerate everything.
/// Safe under Strict (semantically equivalent for any IEnumerable).
/// </summary>
public class EqualsCountZeroStrategy : BaseBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		if (TryGetCountSource(context.Left.Syntax, out var source)
		    && context.Right.Syntax.IsNumericZero())
		{
			optimized = LogicalNotExpression(
				ParenthesizedExpression(
					InvocationExpression(
						MemberAccessExpression(
							SyntaxKind.SimpleMemberAccessExpression,
							source,
							IdentifierName("Any")))));
			return true;
		}

		if (TryGetCountSource(context.Right.Syntax, out source)
		    && context.Left.Syntax.IsNumericZero())
		{
			optimized = LogicalNotExpression(
				ParenthesizedExpression(
					InvocationExpression(
						MemberAccessExpression(
							SyntaxKind.SimpleMemberAccessExpression,
							source,
							IdentifierName("Any")))));
			return true;
		}

		optimized = null;
		return false;
	}

	private static bool TryGetCountSource(ExpressionSyntax expr, out ExpressionSyntax source)
	{
		source = null!;

		if (expr is not InvocationExpressionSyntax
		    {
			    Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Count" } memberAccess,
			    ArgumentList.Arguments.Count: 0
		    })
		{
			return false;
		}

		source = memberAccess.Expression;
		return true;
	}
}

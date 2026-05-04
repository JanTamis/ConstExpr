using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LessThanOrEqualStrategies;

/// <summary>
/// Strategy for Count() &lt;= 0 → !source.Any().
/// Any() short-circuits after the first element; Count() must enumerate everything.
/// Safe under Strict (semantically equivalent for any IEnumerable where Count() &gt;= 0).
/// </summary>
public class LessThanOrEqualCountZeroStrategy : BaseBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// Count() <= 0 → !source.Any()
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

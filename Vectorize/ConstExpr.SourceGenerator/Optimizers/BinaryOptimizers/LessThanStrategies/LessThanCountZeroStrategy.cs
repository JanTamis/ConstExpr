using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LessThanStrategies;

/// <summary>
/// Strategy for 0 &lt; Count() → source.Any().
/// Any() short-circuits after the first element; Count() must enumerate everything.
/// Safe under Strict (semantically equivalent for any IEnumerable).
/// </summary>
public class LessThanCountZeroStrategy : BaseBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// 0 < Count() → source.Any()
		if (context.Left.Syntax.IsNumericZero()
		    && TryGetCountSource(context.Right.Syntax, out var source))
		{
			optimized = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					source,
					IdentifierName("Any")));
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


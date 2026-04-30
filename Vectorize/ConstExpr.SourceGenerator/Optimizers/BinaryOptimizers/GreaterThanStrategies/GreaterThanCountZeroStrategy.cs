using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.GreaterThanStrategies;

/// <summary>
/// Strategy for Count() &gt; 0 → source.Any().
/// Any() short-circuits after the first element; Count() must enumerate everything.
/// Safe under Strict (semantically equivalent for any IEnumerable).
/// </summary>
public class GreaterThanCountZeroStrategy : BaseBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// Count() > 0 → source.Any()
		if (TryGetCountSource(context.Left.Syntax, out var source)
		    && context.Right.Syntax.IsNumericZero())
		{
			optimized = InvocationExpression(
				MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					source,
					IdentifierName("Any")));
			return true;
		}

		// 0 > Count() is always false, but we let the constant-folding handle that.
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

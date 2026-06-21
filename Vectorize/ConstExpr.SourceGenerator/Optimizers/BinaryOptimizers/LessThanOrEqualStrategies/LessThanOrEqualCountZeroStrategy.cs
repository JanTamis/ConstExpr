using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.LessThanOrEqualStrategies;

/// <summary>
///   Strategy for Count() &lt;= 0 → !source.Any() and Count(predicate) &lt;= 0 → !source.Any(predicate).
///   Any() short-circuits after the first element; Count() must enumerate everything.
///   Safe under Strict (semantically equivalent for any IEnumerable where Count() &gt;= 0).
/// </summary>
public class LessThanOrEqualCountZeroStrategy : BaseBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// Count() <= 0 → !source.Any()
		// Count(predicate) <= 0 → !source.Any(predicate)
		if (TryGetCountSourceAndPredicate(context.Left.Syntax, out var source, out var predicate)
		    && context.Right.Syntax.IsNumericZero())
		{
			optimized = LogicalNotExpression(ParenthesizedExpression(BuildAnyCall(source, predicate)));
			return true;
		}

		optimized = null;
		return false;
	}

	private static bool TryGetCountSourceAndPredicate(ExpressionSyntax expr, out ExpressionSyntax source, out ExpressionSyntax? predicate)
	{
		source = null!;
		predicate = null;

		if (expr is not InvocationExpressionSyntax
		    {
			    Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Count" } memberAccess,
			    ArgumentList.Arguments: var args
		    } || args.Count > 1)
		{
			return false;
		}

		source = memberAccess.Expression;
		predicate = args.Count == 1 ? args[0].Expression : null;
		return true;
	}

	private static InvocationExpressionSyntax BuildAnyCall(ExpressionSyntax source, ExpressionSyntax? predicate)
	{
		var memberAccess = MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, source, IdentifierName("Any"));
		return predicate != null
			? InvocationExpression(memberAccess).WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(predicate))))
			: InvocationExpression(memberAccess);
	}
}
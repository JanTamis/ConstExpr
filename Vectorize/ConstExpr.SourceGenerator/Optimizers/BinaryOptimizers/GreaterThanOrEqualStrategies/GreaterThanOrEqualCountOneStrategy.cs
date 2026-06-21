using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.Strategies;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Optimizers.BinaryOptimizers.GreaterThanOrEqualStrategies;

/// <summary>
///   Strategy for Count() >= 1 → source.Any() and Count(predicate) >= 1 → source.Any(predicate).
///   Any() short-circuits after the first element; Count() must enumerate everything.
///   Safe under Strict (semantically equivalent for any IEnumerable).
/// </summary>
public class GreaterThanOrEqualCountOneStrategy : BaseBinaryStrategy
{
	public override bool TryOptimize(BinaryOptimizeContext<ExpressionSyntax, ExpressionSyntax> context, out ExpressionSyntax? optimized)
	{
		// Count() >= 1 → source.Any()
		// Count(predicate) >= 1 → source.Any(predicate)
		if (TryGetCountSourceAndPredicate(context.Left.Syntax, out var source, out var predicate)
		    && context.Right.Syntax.IsNumericOne())
		{
			optimized = BuildAnyCall(source, predicate);
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
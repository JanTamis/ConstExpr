using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Rewrites indexing off the end of a collection into index-from-end syntax:
///   <code>
///   arr[arr.Length - 1 - i]
///   =>
///   arr[^(1 + i)]
///   </code>
///   Matches any subtraction chain rooted at <c>receiver.Length</c> or <c>receiver.Count</c>
///   (so <c>arr[arr.Length - i]</c> and <c>arr[arr.Length - 1]</c> are handled too), as long as
///   the indexed receiver and the <c>Length</c>/<c>Count</c> receiver are structurally identical
///   and pure — otherwise collapsing the two occurrences into one could change how many times a
///   side-effecting receiver is evaluated. The original code already indexes the receiver with an
///   <c>int</c>, so it necessarily satisfies the compiler's pattern-based requirements for <c>^</c>.
/// </summary>
public sealed class IndexFromEndRewriter : CSharpSyntaxRewriter
{
	public static SyntaxNode Apply(SyntaxNode node)
	{
		return new IndexFromEndRewriter().Visit(node);
	}

	public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
	{
		var visited = (ElementAccessExpressionSyntax) base.VisitElementAccessExpression(node)!;

		if (visited.ArgumentList.Arguments is not [ { NameColon: null, RefKindKeyword.RawKind: 0 } argument ]
		    || !LoopInvariance.IsPureExpression(visited.Expression)
		    || !TryMatchLengthMinusOffset(argument.Expression, visited.Expression, out var offset))
		{
			return visited;
		}

		return visited.WithArgumentList(
			BracketedArgumentList(SingletonSeparatedList(Argument(IndexFromEndExpression(ParenthesizeIfNeeded(offset))))));
	}

	/// <summary>
	///   Recursively unwraps a subtraction chain looking for <c>receiver.Length</c>/<c>.Count</c>
	///   at its root, accumulating everything subtracted after it into <paramref name="offset" />.
	/// </summary>
	private static bool TryMatchLengthMinusOffset(ExpressionSyntax expr, ExpressionSyntax receiver, out ExpressionSyntax offset)
	{
		if (Unwrap(expr) is BinaryExpressionSyntax subtract && subtract.IsKind(SyntaxKind.SubtractExpression))
		{
			if (IsLengthOrCountOf(subtract.Left, receiver))
			{
				offset = subtract.Right;
				return true;
			}

			if (TryMatchLengthMinusOffset(subtract.Left, receiver, out var innerOffset))
			{
				offset = BinaryExpression(SyntaxKind.AddExpression, innerOffset, subtract.Right);
				return true;
			}
		}

		offset = null!;
		return false;
	}

	// ponytail: matches the property name only, not its type or that an int indexer exists.
	// Safe here because the original code already indexed the receiver with an int, which implies
	// both; add a semantic-model guard if this is ever applied beyond arrays/List/Span/string.
	private static bool IsLengthOrCountOf(ExpressionSyntax expr, ExpressionSyntax receiver)
	{
		return Unwrap(expr) is MemberAccessExpressionSyntax { Name.Identifier.Text: "Length" or "Count" } member
		       && member.Expression.NormalizeWhitespace().ToFullString() == receiver.NormalizeWhitespace().ToFullString();
	}

	private static ExpressionSyntax Unwrap(ExpressionSyntax expr)
	{
		while (expr is ParenthesizedExpressionSyntax paren)
		{
			expr = paren.Expression;
		}

		return expr;
	}

	// The index-from-end operator binds tighter than binary operators, so a compound offset
	// (e.g. "1 + i") needs explicit parens; an already-atomic offset doesn't.
	private static ExpressionSyntax ParenthesizeIfNeeded(ExpressionSyntax expr)
	{
		return expr is LiteralExpressionSyntax or IdentifierNameSyntax or MemberAccessExpressionSyntax or ParenthesizedExpressionSyntax
			? expr
			: ParenthesizedExpression(expr);
	}
}
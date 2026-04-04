using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that applies De Morgan's law to logical binary expressions.
/// Inspired by the Roslyn <c>InvertLogicalCodeRefactoringProvider</c>.
///
/// <list type="bullet">
///   <item><c>a &amp;&amp; b</c>  →  <c>!(!a || !b)</c>  (simplified)</item>
///   <item><c>a || b</c>  →  <c>!(!a &amp;&amp; !b)</c>  (simplified)</item>
/// </list>
///
/// More precisely, the produced form is the DeMorgan-equivalent, then wrapped in an outer
/// negation that cancels out, so the overall expression is semantically identical.
/// </summary>
public static class InvertLogicalRefactoring
{
	/// <summary>
	/// Tries to apply De Morgan's law to a logical <c>&amp;&amp;</c> or <c>||</c> expression.
	/// The result is a semantically equivalent expression with the operator flipped and each
	/// operand negated, wrapped in an outer <c>!(…)</c>.
	/// </summary>
	public static bool TryInvertLogical(
		BinaryExpressionSyntax node,
		[NotNullWhen(true)] out ExpressionSyntax? result)
	{
		result = null;

		if (!node.IsKind(SyntaxKind.LogicalAndExpression) 
		    && !node.IsKind(SyntaxKind.LogicalOrExpression))
		{
			return false;
		}

		var negatedLeft = NegateExpressionRefactoring.Negate(node.Left);
		var negatedRight = NegateExpressionRefactoring.Negate(node.Right);

		var flippedKind = node.IsKind(SyntaxKind.LogicalAndExpression)
			? SyntaxKind.LogicalOrExpression
			: SyntaxKind.LogicalAndExpression;

		var inner = BinaryExpression(flippedKind, negatedLeft, negatedRight);

		// Wrap in !(...) to preserve overall truth value.
		result = PrefixUnaryExpression(
			SyntaxKind.LogicalNotExpression,
			ParenthesizedExpression(inner));

		return true;
	}
}


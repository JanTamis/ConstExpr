using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
///   Refactorer that applies De Morgan's law to logical binary expressions.
///   Inspired by the Roslyn <c>InvertLogicalCodeRefactoringProvider</c>.
///   <list type="bullet">
///     <item><c>a &amp;&amp; b</c>  →  <c>!(!a || !b)</c>  (simplified)</item>
///     <item><c>a || b</c>  →  <c>!(!a &amp;&amp; !b)</c>  (simplified)</item>
///   </list>
///   More precisely, the produced form is the DeMorgan-equivalent, then wrapped in an outer
///   negation that cancels out, so the overall expression is semantically identical.
/// </summary>
public static class InvertLogicalRefactoring
{
	/// <summary>
	///   Tries to apply De Morgan's law to a logical <c>&amp;&amp;</c> or <c>||</c> expression.
	///   The result is a semantically equivalent expression with the operator flipped and each
	///   operand negated, wrapped in an outer <c>!(…)</c>.
	/// </summary>
	public static bool TryInvertLogical(
		BinaryExpressionSyntax? node,
		[NotNullWhen(true)] out ExpressionSyntax? result,
		bool allowRelational = true)
	{
		result = null;

		if (node is null)
		{
			return false;
		}

		result = node.Kind() switch
		{
			SyntaxKind.LogicalAndExpression => ParenthesizedExpression(LogicalOrExpression(NegateExpressionRefactoring.Negate(node.Left, allowRelational), NegateExpressionRefactoring.Negate(node.Right, allowRelational))),
			SyntaxKind.LogicalOrExpression => LogicalAndExpression(NegateExpressionRefactoring.Negate(node.Left, allowRelational), NegateExpressionRefactoring.Negate(node.Right, allowRelational)),
			// equality inversion keeps its operands and is NaN-safe: !(NaN == x) and NaN != x are both true
			SyntaxKind.EqualsExpression => NotEqualsExpression(node.Left, node.Right),
			SyntaxKind.NotEqualsExpression => EqualsExpression(node.Left, node.Right),
			// relational inversion flips the outcome for NaN operands - callers gate via allowRelational
			SyntaxKind.GreaterThanExpression when allowRelational => LessThanOrEqualExpression(node.Left, node.Right),
			SyntaxKind.GreaterThanOrEqualExpression when allowRelational => LessThanExpression(node.Left, node.Right),
			SyntaxKind.LessThanExpression when allowRelational => GreaterThanOrEqualExpression(node.Left, node.Right),
			SyntaxKind.LessThanOrEqualExpression when allowRelational => GreaterThanExpression(node.Left, node.Right),
			// unhandled operators must keep their negation - never silently drop the '!'
			_ => null
		};

		return result is not null;
	}
}
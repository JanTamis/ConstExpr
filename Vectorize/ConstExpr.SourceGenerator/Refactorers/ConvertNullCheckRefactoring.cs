using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts a null-conditional chain to an explicit null check,
/// and vice versa.
/// Inspired by Roslyn's pattern-matching and null-check related refactorings.
///
/// <list type="bullet">
///   <item><c>obj?.Method()</c>  →  <c>if (obj != null) { obj.Method(); }</c></item>
///   <item><c>obj?.Property ?? defaultValue</c>  →  <c>obj != null ? obj.Property : defaultValue</c></item>
/// </list>
/// </summary>
public static class ConvertNullCheckRefactoring
{
	/// <summary>
	/// Converts a null-coalescing expression <c>expr ?? defaultExpr</c> into a ternary
	/// null-check: <c>expr != null ? expr : defaultExpr</c>.
	/// </summary>
	public static bool TryConvertNullCoalescingToConditional(
		BinaryExpressionSyntax coalescing,
		[NotNullWhen(true)] out ConditionalExpressionSyntax? result)
	{
		result = null;

		if (!coalescing.IsKind(SyntaxKind.CoalesceExpression))
		{
			return false;
		}

		var left = coalescing.Left;
		var right = coalescing.Right;

		// Build: left != null ? left : right
		var condition = BinaryExpression(
			SyntaxKind.NotEqualsExpression,
			left,
			LiteralExpression(SyntaxKind.NullLiteralExpression));

		result = ConditionalExpression(condition, left, right);
		return true;
	}

	/// <summary>
	/// Converts a ternary null-check <c>x != null ? x : defaultExpr</c> (or <c>x is not null ? x : defaultExpr</c>)
	/// into a null-coalescing expression <c>x ?? defaultExpr</c>.
	/// </summary>
	public static bool TryConvertConditionalToNullCoalescing(
		ConditionalExpressionSyntax conditional,
		[NotNullWhen(true)] out BinaryExpressionSyntax? result)
	{
		result = null;

		switch (conditional.Condition)
		{
			// Pattern: x != null ? x : default
			case BinaryExpressionSyntax { RawKind: (int)SyntaxKind.NotEqualsExpression } neq:
			{
				ExpressionSyntax? testedExpr = null;

				if (IsNullLiteral(neq.Right))
				{
					testedExpr = neq.Left;
				}
				else if (IsNullLiteral(neq.Left))
				{
					testedExpr = neq.Right;
				}

				if (testedExpr is not null &&
				    AreEquivalent(testedExpr, conditional.WhenTrue))
				{
					result = BinaryExpression(
						SyntaxKind.CoalesceExpression,
						testedExpr,
						conditional.WhenFalse);
					return true;
				}
				break;
			}
			// Pattern: x is not null ? x : default
			case IsPatternExpressionSyntax
				{
					Pattern: UnaryPatternSyntax
					{
						RawKind: (int)SyntaxKind.NotPattern,
						Pattern: ConstantPatternSyntax { Expression: LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression } }
					}
				} isPattern
				when AreEquivalent(isPattern.Expression, conditional.WhenTrue):
			{
				result = BinaryExpression(
					SyntaxKind.CoalesceExpression,
					isPattern.Expression,
					conditional.WhenFalse);
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Converts a null-coalescing assignment <c>x ??= value</c> into an explicit if-null assignment:
	/// <c>if (x is null) x = value;</c>
	/// </summary>
	public static bool TryConvertNullCoalescingAssignmentToIfNull(
		ExpressionStatementSyntax expressionStatement,
		[NotNullWhen(true)] out IfStatementSyntax? result,
		SemanticModel? semanticModel = null)
	{
		result = null;

		if (expressionStatement.Expression is not AssignmentExpressionSyntax
		    {
			    RawKind: (int)SyntaxKind.CoalesceAssignmentExpression
		    } assignment)
		{
			return false;
		}

		var target = assignment.Left;
		var value = assignment.Right;

		var condition = IsPatternExpression(
			target,
			ConstantPattern(LiteralExpression(SyntaxKind.NullLiteralExpression)));

		var body = ExpressionStatement(
			AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, target, value));

		result = IfStatement(condition, body);
		return true;
	}

	private static bool IsNullLiteral(ExpressionSyntax expr)
	{
		return expr is LiteralExpressionSyntax { RawKind: (int)SyntaxKind.NullLiteralExpression };
	}

	private static bool AreEquivalent(ExpressionSyntax a, ExpressionSyntax b)
	{
		return a.GetDeterministicHash() == b.GetDeterministicHash();
	}
}


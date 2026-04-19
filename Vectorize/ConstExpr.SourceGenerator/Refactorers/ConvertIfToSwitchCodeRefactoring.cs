using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Comparers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

public static class ConvertIfToSwitchCodeRefactoring
{
	/// <summary>
	/// Tries to convert an if-else-if chain into a switch statement or switch expression.
	/// The chain is eligible when every condition tests the same identifier against a
	/// constant-like value using == (or an is-pattern with constant / or-pattern).
	///
	/// Priority:
	///   1. switch expression  — when every branch is a single <c>return expr;</c>
	///      and an else clause (default) is present.
	///   2. switch statement   — for all other eligible chains.
	///
	/// Minimum requirement: at least 2 if-branches (to avoid generating a noisier one-case
	/// switch for a trivial if/else).
	/// </summary>
	public static bool TryConvertIfElseChainToSwitch(
		IfStatementSyntax node,
		[NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;
		ExpressionSyntax? switchTarget = null;
		var sections = new List<(List<ExpressionSyntax> Labels, StatementSyntax Body)>();

		// Walk the if / else-if chain.
		SyntaxNode? current = node;

		while (current is IfStatementSyntax ifNode)
		{
			if (!TryExtractSwitchLabels(ifNode.Condition, ref switchTarget, out var labels))
			{
				return false;
			}

			sections.Add((labels, ifNode.Statement));
			current = ifNode.Else?.Statement;
		}

		// Need at least 2 if-branches to be worth converting.
		if (sections.Count < 2)
		{
			return false;
		}

		if (switchTarget is null)
		{
			return false;
		}

		// The remaining node (if any) becomes the default case.
		var defaultBody = current as StatementSyntax;

		// --- Prefer switch expression when all branches are pure returns ---
		if (defaultBody is not null
		    && TryBuildSwitchExpression(switchTarget, sections, defaultBody, out var switchExpr))
		{
			result = switchExpr;
			return true;
		}

		// --- Fall back to switch statement ---
		var switchSections = new List<SwitchSectionSyntax>();

		foreach (var (labels, body) in sections)
		{
			var caseLabel = BuildCaseSwitchLabel(labels);

			switchSections.Add(SwitchSection(
				List([ caseLabel ]),
				List(BuildSwitchSectionStatements(body))));
		}

		if (defaultBody is not null)
		{
			switchSections.Add(SwitchSection(
				List<SwitchLabelSyntax>([ DefaultSwitchLabel() ]),
				List(BuildSwitchSectionStatements(defaultBody))));
		}

		result = SwitchStatement(switchTarget, List(switchSections));
		return true;
	}

	/// <summary>
	/// Tries to build a <c>return x switch { ... };</c> statement.
	/// Succeeds only when every section (and the default body) contains a single
	/// <c>return expr;</c> statement.
	/// </summary>
	private static bool TryBuildSwitchExpression(
		ExpressionSyntax switchTarget,
		List<(List<ExpressionSyntax> Labels, StatementSyntax Body)> sections,
		StatementSyntax defaultBody,
		[NotNullWhen(true)] out StatementSyntax? result)
	{
		result = null;
		var arms = new List<SwitchExpressionArmSyntax>();

		foreach (var (labels, body) in sections)
		{
			if (!TryGetSingleReturnExpression(body, out var returnExpr))
			{
				return false;
			}

			PatternSyntax pattern = ConstantPattern(labels[0]);

			for (var i = 1; i < labels.Count; i++)
			{
				pattern = BinaryPattern(
					SyntaxKind.OrPattern,
					pattern,
					ConstantPattern(labels[i]));
			}

			arms.Add(SwitchExpressionArm(pattern, returnExpr));
		}

		if (!TryGetSingleReturnExpression(defaultBody, out var defaultExpr))
		{
			return false;
		}

		arms.Add(SwitchExpressionArm(DiscardPattern(), defaultExpr));

		result = ReturnStatement(
			SwitchExpression(
				switchTarget,
				SeparatedList(arms)));

		return true;
	}

	/// <summary>
	/// Returns <see langword="true"/> when <paramref name="body"/> is (or wraps) a single
	/// <c>return expr;</c> statement, and sets <paramref name="expression"/> to the returned
	/// expression.
	/// </summary>
	private static bool TryGetSingleReturnExpression(
		StatementSyntax body,
		[NotNullWhen(true)] out ExpressionSyntax? expression)
	{
		expression = null;

		return body switch
		{
			ReturnStatementSyntax { Expression: { } expr }
				=> (expression = expr) is not null,
			BlockSyntax { Statements: [ ReturnStatementSyntax { Expression: { } expr } ] }
				=> (expression = expr) is not null,
			_ => false
		};
	}

	/// <summary>
	/// Extracts one or more switch case labels from an if-condition.
	/// Handles:
	///   <c>x == constant</c> / <c>constant == x</c>
	///   <c>x == a || x == b</c>
	///   <c>x is constant</c>
	///   <c>x is (A or B)</c>
	/// </summary>
	private static bool TryExtractSwitchLabels(
		ExpressionSyntax condition,
		ref ExpressionSyntax? switchTarget,
		[NotNullWhen(true)] out List<ExpressionSyntax>? labels)
	{
		labels = null;
		var collected = new List<ExpressionSyntax>();

		if (!TryCollectLabels(condition, ref switchTarget, collected) 
		    || collected.Count == 0)
		{
			return false;
		}

		labels = collected;
		return true;
	}

	private static bool TryCollectLabels(
		ExpressionSyntax condition,
		ref ExpressionSyntax? switchTarget,
		List<ExpressionSyntax> labels)
	{
		// x == a || x == b
		if (condition is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.LogicalOrExpression } orExpr)
		{
			return TryCollectLabels(orExpr.Left, ref switchTarget, labels)
			       && TryCollectLabels(orExpr.Right, ref switchTarget, labels);
		}

		// x == constant  /  constant == x
		if (condition is BinaryExpressionSyntax { RawKind: (int)SyntaxKind.EqualsExpression } eq)
		{
			ExpressionSyntax? target = null;
			ExpressionSyntax? label = null;

			if (IsConstantLike(eq.Right))
			{
				target = eq.Left;
				label = eq.Right;
			}
			else if (IsConstantLike(eq.Left))
			{
				target = eq.Right;
				label = eq.Left;
			}

			if (target is null || label is null)
			{
				return false;
			}

			if (switchTarget is not null && !SyntaxNodeComparer.Get().Equals(switchTarget, target))
			{
				return false;
			}

			switchTarget = target;
			labels.Add(label);
			return true;
		}

		// x is constant  /  x is (A or B)
		if (condition is IsPatternExpressionSyntax { Expression: IdentifierNameSyntax targetExpr } isExpr)
		{
			if (switchTarget is not null && !SyntaxNodeComparer.Get().Equals(switchTarget, targetExpr))
			{
				return false;
			}

			if (!TryCollectPatternLabels(isExpr.Pattern, labels))
			{
				return false;
			}

			switchTarget = targetExpr;
			return true;
		}

		return false;
	}

	private static bool TryCollectPatternLabels(PatternSyntax pattern, List<ExpressionSyntax> labels)
	{
		switch (pattern)
		{
			case ConstantPatternSyntax constPat:
			{
				labels.Add(constPat.Expression);
				return true;
			}

			case BinaryPatternSyntax orPat when orPat.IsKind(SyntaxKind.OrPattern):
			{
				return TryCollectPatternLabels(orPat.Left, labels)
				       && TryCollectPatternLabels(orPat.Right, labels);
			}

			case ParenthesizedPatternSyntax paren:
			{
				return TryCollectPatternLabels(paren.Pattern, labels);
			}

			default:
			{
				return false;
			}
		}
	}

	/// <summary>
	/// Returns <see langword="true"/> when the expression is constant-like and therefore safe
	/// to use as a switch case label (literal, negative literal, enum member access).
	/// </summary>
	private static bool IsConstantLike(ExpressionSyntax expr)
	{
		return expr is LiteralExpressionSyntax
			or MemberAccessExpressionSyntax // e.g. Color.Red
			or PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int) SyntaxKind.MinusToken };
		// -1
	}

	/// <summary>
	/// Builds the statement list for a single switch section, automatically appending a
	/// <c>break;</c> when the body does not already end with a jump statement.
	/// </summary>
	private static List<StatementSyntax> BuildSwitchSectionStatements(StatementSyntax body)
	{
		var result = new List<StatementSyntax>();

		if (body is BlockSyntax block)
		{
			result.AddRange(block.Statements);
		}
		else
		{
			result.Add(body);
		}

		if (result.Count == 0 || !ContainsJumpStatement(result[^1]))
		{
			result.Add(BreakStatement());
		}

		return result;
	}

	/// <summary>
	/// Builds a single switch label for a set of case values.
	/// <list type="bullet">
	///   <item>Single value   → <c>case 1:</c></item>
	///   <item>Multiple values → <c>case 1 or 2 or 3:</c></item>
	/// </list>
	/// </summary>
	private static SwitchLabelSyntax BuildCaseSwitchLabel(List<ExpressionSyntax> labels)
	{
		if (labels.Count == 1)
		{
			return CaseSwitchLabel(labels[0]);
		}

		// Build an or-pattern: case 1 or 2 or 3:
		PatternSyntax pattern = ConstantPattern(labels[0]);

		for (var i = 1; i < labels.Count; i++)
		{
			pattern = BinaryPattern(
				SyntaxKind.OrPattern,
				pattern,
				ConstantPattern(labels[i]));
		}

		return CasePatternSwitchLabel(pattern, Token(SyntaxKind.ColonToken));
	}

	/// <summary>
	/// Returns <see langword="true"/> when a statement unconditionally ends with a jump
	/// (return, break, continue, or throw), making it safe to combine consecutive if
	/// statements with identical bodies using <c>||</c>.
	/// </summary>
	private static bool ContainsJumpStatement(StatementSyntax statement)
	{
		return statement switch
		{
			ReturnStatementSyntax => true,
			BreakStatementSyntax => true,
			ContinueStatementSyntax => true,
			ThrowStatementSyntax => true,
			BlockSyntax block => block.Statements.Count > 0 && ContainsJumpStatement(block.Statements.Last()),
			_ => false
		};
	}
}
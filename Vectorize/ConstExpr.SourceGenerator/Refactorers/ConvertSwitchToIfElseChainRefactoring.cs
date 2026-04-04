using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that converts a switch statement or switch expression back into
/// an if-else-if chain.
/// This is the inverse of <see cref="ConvertIfToSwitchCodeRefactoring"/>.
///
/// Inspired by Roslyn's general switch ↔ if conversion patterns.
/// </summary>
public static class ConvertSwitchToIfElseChainRefactoring
{
	// -----------------------------------------------------------------------
	// Switch statement → if/else chain
	// -----------------------------------------------------------------------

	/// <summary>
	/// Converts a switch statement into an if-else-if chain.
	/// Requires at least two sections; the <c>default</c> section (if any) becomes the final <c>else</c>.
	/// </summary>
	public static bool TryConvertSwitchStatementToIfElse(
		SwitchStatementSyntax switchStatement,
		[NotNullWhen(true)] out IfStatementSyntax? result)
	{
		result = null;

		if (switchStatement.Sections.Count < 2)
		{
			return false;
		}

		var governingExpr = switchStatement.Expression;

		StatementSyntax? defaultBody = null;
		var branches = new List<(ExpressionSyntax Condition, StatementSyntax Body)>();

		foreach (var section in switchStatement.Sections)
		{
			var conditions = new List<ExpressionSyntax>();
			var hasDefault = false;

			foreach (var label in section.Labels)
			{
				switch (label)
				{
					case CaseSwitchLabelSyntax caseLabel:
					{
						conditions.Add(EqualsExpression(governingExpr, caseLabel.Value));
						break;
					}

					case CasePatternSwitchLabelSyntax patternLabel:
					{
						if (!TryExtractConditionsFromPattern(governingExpr, patternLabel.Pattern, conditions))
						{
							return false;
						}

						break;
					}

					case DefaultSwitchLabelSyntax:
					{
						hasDefault = true;
						break;
					}

					default:
					{
						return false;
					}
				}
			}

			var body = BuildBodyFromSwitchSection(section);

			if (hasDefault && conditions.Count == 0)
			{
				defaultBody = body;
			}
			else if (conditions.Count > 0)
			{
				var combinedCondition = conditions.Aggregate(LogicalOrExpression);
				branches.Add((combinedCondition, body));
			}
			else
			{
				return false;
			}
		}

		if (branches.Count < 1)
		{
			return false;
		}

		// Build the chain from bottom up
		IfStatementSyntax? current = null;

		for (var i = branches.Count - 1; i >= 0; i--)
		{
			var (condition, body) = branches[i];
			ElseClauseSyntax? elseClause = null;

			if (current is not null)
			{
				elseClause = ElseClause(current);
			}
			else if (defaultBody is not null && i == branches.Count - 1)
			{
				elseClause = ElseClause(defaultBody);
			}

			current = IfStatement(condition, body, elseClause);
		}

		result = current;
		return result is not null;
	}

	// -----------------------------------------------------------------------
	// Switch expression → conditional (ternary) chain
	// -----------------------------------------------------------------------

	/// <summary>
	/// Converts a switch expression into a chain of ternary conditional expressions.
	/// </summary>
	public static bool TryConvertSwitchExpressionToConditionals(
		SwitchExpressionSyntax switchExpression,
		[NotNullWhen(true)] out ExpressionSyntax? result)
	{
		result = null;

		if (switchExpression.Arms.Count < 2)
		{
			return false;
		}

		var governingExpr = switchExpression.GoverningExpression;

		// Build from right to left
		ExpressionSyntax? accumulator = null;

		for (var i = switchExpression.Arms.Count - 1; i >= 0; i--)
		{
			var arm = switchExpression.Arms[i];

			// Discard pattern (_) is the default
			if (arm.Pattern is DiscardPatternSyntax)
			{
				accumulator = arm.Expression;
				continue;
			}

			var conditions = new List<ExpressionSyntax>();

			if (!TryExtractConditionsFromPattern(governingExpr, arm.Pattern, conditions))
			{
				return false;
			}

			if (conditions.Count == 0)
			{
				return false;
			}

			var combinedCondition = conditions.Aggregate(LogicalOrExpression);

			if (accumulator is null)
			{
				// No default — cannot fully convert
				return false;
			}

			accumulator = ConditionalExpression(combinedCondition, arm.Expression, accumulator);
		}

		result = accumulator;
		return result is not null;
	}

	// -----------------------------------------------------------------------
	// Private helpers
	// -----------------------------------------------------------------------

	private static bool TryExtractConditionsFromPattern(
		ExpressionSyntax governing,
		PatternSyntax pattern,
		List<ExpressionSyntax> conditions)
	{
		switch (pattern)
		{
			case ConstantPatternSyntax constant:
			{
				conditions.Add(EqualsExpression(governing, constant.Expression));
				return true;
			}

			case BinaryPatternSyntax { RawKind: (int)SyntaxKind.OrPattern } orPattern:
			{
				return TryExtractConditionsFromPattern(governing, orPattern.Left, conditions) 
				       && TryExtractConditionsFromPattern(governing, orPattern.Right, conditions);
			}

			case ParenthesizedPatternSyntax paren:
			{
				return TryExtractConditionsFromPattern(governing, paren.Pattern, conditions);
			}

			default:
			{
				return false;
			}
		}
	}

	/// <summary>
	/// Builds a block from a switch section's statements, removing any trailing <c>break;</c>.
	/// </summary>
	private static StatementSyntax BuildBodyFromSwitchSection(SwitchSectionSyntax section)
	{
		var statements = section.Statements
			.Where(s => s is not BreakStatementSyntax)
			.ToList();

		return statements.Count == 1
			? statements[0]
			: Block(statements);
	}
}


using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Statement visitor methods for the ConstExprPartialRewriter.
/// Handles if, for, foreach, while, switch, return, block, and local declaration statements.
/// </summary>
public partial class ConstExprPartialRewriter
{
	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		var condition = Visit(node.Condition);

		if (TryGetLiteralValue(condition, out var value))
		{
			if (value is true)
			{
				return Visit(node.Statement);
			}

			return node.Else is not null
				? Visit(node.Else.Statement)
				: null;
		}

		var statement = Visit(node.Statement);
		var @else = Visit(node.Else);

		var result = node
			.WithCondition(condition as ExpressionSyntax ?? node.Condition)
			.WithStatement(statement as StatementSyntax ?? node.Statement)
			.WithElse(@else as ElseClauseSyntax);

		// Try to convert an if-else-if chain to a switch statement / switch expression.
		// Only attempt this when there is at least one else branch (otherwise a single-case
		// switch would be noisier than the original if).
		if (result.Else is not null && TryConvertIfElseChainToSwitch(result, out var switchNode))
		{
			return switchNode;
		}

		return result;
	}

	public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
	{
		var names = variables.Keys.ToImmutableHashSet();

		Visit(node.Declaration);

		var condition = Visit(node.Condition);

		if (TryGetLiteralValue(condition, out var value))
		{
			switch (value)
			{
				case false:
					return null;
				case true:
					return TryUnrollForLoop(node);
			}
		}

		return VisitForStatementWithoutUnroll(node, names);
	}

	/// <summary>
	/// Tries to unroll a for loop when the condition is always true.
	/// </summary>
	private SyntaxNode? TryUnrollForLoop(ForStatementSyntax node)
	{
		if (attribute.MaxUnrollIterations == 0)
		{
			return base.VisitForStatement(node);
		}

		var result = new List<SyntaxNode?>();
		var iteratorCount = 0;

		do
		{
			if (iteratorCount++ >= attribute.MaxUnrollIterations)
			{
				InvalidateAssignedVariables(node);
				return base.VisitForStatement(node);
			}

			var statement = Visit(node.Statement);

			if (statement is not BlockSyntax)
			{
				result.Add(statement);
			}

			if (ShouldStopUnrolling(statement, result))
			{
				break;
			}

			VisitList(node.Incrementors);
		} while (TryGetLiteralValue(Visit(node.Condition), out var value) && value is true);

		return result.Count > 0 ? ToStatementSyntax(result) : null;
	}

	/// <summary>
	/// Visits a for statement without attempting to unroll it.
	/// </summary>
	private SyntaxNode VisitForStatementWithoutUnroll(ForStatementSyntax node, ImmutableHashSet<string> names)
	{
		// Restore variable states after visiting the loop
		foreach (var name in variables.Keys.Except(names).ToList())
		{
			variables.Remove(name);
		}

		var declaration = Visit(node.Declaration);
		InvalidateAssignedVariables(node);

		return node
			.WithInitializers(VisitList(node.Initializers))
			.WithCondition(Visit(node.Condition) as ExpressionSyntax ?? node.Condition)
			.WithDeclaration(declaration as VariableDeclarationSyntax ?? node.Declaration)
			.WithStatement(Visit(node.Statement) as StatementSyntax ?? node.Statement);
	}

	public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
	{
		var condition = Visit(node.Condition);

		if (TryGetLiteralValue(condition, out var value))
		{
			if (value is false)
			{
				return null;
			}

			if (value is true && attribute.MaxUnrollIterations > 0)
			{
				return TryUnrollWhileLoop(node);
			}
		}

		InvalidateAssignedVariables(node);
		return base.VisitWhileStatement(node);
	}

	public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
	{
		// Do-while always executes at least once
		var result = new List<SyntaxNode?>();
		var iteratorCount = 0;

		do
		{
			if (iteratorCount++ >= attribute.MaxUnrollIterations)
			{
				InvalidateAssignedVariables(node);
				return base.VisitDoStatement(node);
			}

			var statement = Visit(node.Statement);

			if (statement is not BlockSyntax)
			{
				result.Add(statement);
			}

			if (ShouldStopUnrolling(statement, result))
			{
				break;
			}

			if (statement is BlockSyntax block)
			{
				result.AddRange(block.Statements);
			}
		} while (TryGetLiteralValue(Visit(node.Condition), out var value) && value is true);

		// Check final condition
		var finalCondition = Visit(node.Condition);

		if (TryGetLiteralValue(finalCondition, out var finalValue) && finalValue is false)
		{
			return result.Count > 0 ? ToStatementSyntax(result) : null;
		}

		InvalidateAssignedVariables(node);
		
		return base.VisitDoStatement(node);
	}

	/// <summary>
	/// Tries to unroll a while loop when the condition is always true.
	/// </summary>
	private SyntaxNode? TryUnrollWhileLoop(WhileStatementSyntax node)
	{
		var result = new List<SyntaxNode?>();
		var iteratorCount = 0;

		do
		{
			if (iteratorCount++ >= attribute.MaxUnrollIterations)
			{
				InvalidateAssignedVariables(node);
				return base.VisitWhileStatement(node);
			}

			var statement = Visit(node.Statement);

			if (statement is not BlockSyntax)
			{
				result.Add(statement);
			}

			if (ShouldStopUnrolling(statement, result))
			{
				break;
			}
		} while (TryGetLiteralValue(Visit(node.Condition), out var value) && value is true);

		return result.Count > 0 ? ToStatementSyntax(result) : null;
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		var names = variables.Keys.ToImmutableHashSet();
		var collection = Visit(node.Expression);

		var items = GetForEachItems(collection);

		if (items is not null && attribute.MaxUnrollIterations > 0 && items.Count < attribute.MaxUnrollIterations)
		{
			return TryUnrollForEachLoop(node, items);
		}

		InvalidateAssignedVariablesForForEach(node, names);
		return base.VisitForEachStatement(node);
	}

	/// <summary>
	/// Gets the items from a foreach collection expression.
	/// </summary>
	private IReadOnlyList<CSharpSyntaxNode>? GetForEachItems(SyntaxNode? collection)
	{
		return collection switch
		{
			CollectionExpressionSyntax collectionExpression => collectionExpression.Elements,
			LiteralExpressionSyntax { RawKind: (int)SyntaxKind.StringLiteralExpression } stringLiteral =>
				stringLiteral.Token.ValueText
					.Select(s => CreateLiteral(s) as CSharpSyntaxNode)
					.ToList(),
			_ => null
		};
	}

	/// <summary>
	/// Tries to unroll a foreach loop.
	/// </summary>
	private SyntaxNode? TryUnrollForEachLoop(ForEachStatementSyntax node, IReadOnlyList<CSharpSyntaxNode> items)
	{
		var name = node.Identifier.Text;

		if (semanticModel.GetOperation(node) is not IForEachLoopOperation operation)
		{
			return base.VisitForEachStatement(node);
		}

		var variable = new VariableItem(operation.LoopControlVariable.Type, true, null, true);
		variables.Add(name, variable);

		var statements = new List<SyntaxNode>();

		foreach (var item in items)
		{
			if (!TryGetLiteralValue(item, out var val))
			{
				continue;
			}

			variable.Value = val;
			var statement = Visit(node.Statement);

			if (statement is not BlockSyntax)
			{
				statements.Add(statement);
			}

			if (ShouldStopUnrolling(statement, statements))
			{
				break;
			}

			if (statement is BlockSyntax block)
			{
				statements.Add(block);
			}
		}

		return ToStatementSyntax(statements);
	}

	/// <summary>
	/// Invalidates assigned variables for a foreach loop.
	/// </summary>
	private void InvalidateAssignedVariablesForForEach(ForEachStatementSyntax node, ImmutableHashSet<string> names)
	{
		var assignedVariables = AssignedVariables(node);

		foreach (var name in names)
		{
			if (variables.TryGetValue(name, out var variable) && assignedVariables.Contains(name))
			{
				variable.HasValue = false;
			}
		}
	}

	/// <summary>
	/// Checks if loop unrolling should stop based on the current statement.
	/// </summary>
	private static bool ShouldStopUnrolling(SyntaxNode? statement, ICollection<SyntaxNode?> result)
	{
		if (statement is BreakStatementSyntax or ReturnStatementSyntax)
		{
			return true;
		}

		if (statement is not BlockSyntax block || !block.Statements.Any(s => s is BreakStatementSyntax or ReturnStatementSyntax))
		{
			return false;
		}

		foreach (var item in block.Statements)
		{
			if (item is BreakStatementSyntax)
			{
				return true;
			}

			result.Add(item);

			if (item is ReturnStatementSyntax)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Invalidates all assigned variables in the given node.
	/// </summary>
	private void InvalidateAssignedVariables(StatementSyntax node)
	{
		foreach (var name in AssignedVariables(node))
		{
			if (variables.TryGetValue(name, out var variable))
			{
				variable.HasValue = false;
			}
		}
	}

	public override SyntaxNode VisitBlock(BlockSyntax node)
	{
		var visited = VisitList(node.Statements);

		var untilThrown = TakeUntilThrownStatements(visited);
		var combined = CombineConsecutiveIfStatements(untilThrown);
		var simplified = SimplifyIfReturnPatterns(combined);
		
		return node.WithStatements(simplified);
	}

	/// <summary>
	/// Takes statements until a throw statement is encountered (inclusive).
	/// Any code after a throw statement is unreachable and can be removed.
	/// </summary>
	private static SyntaxList<StatementSyntax> TakeUntilThrownStatements(SyntaxList<StatementSyntax> statements)
	{
		var result = new List<StatementSyntax>();

		foreach (var statement in statements)
		{
			result.Add(statement);

			// Stop after a throw statement since code after it is unreachable
			if (statement is ThrowStatementSyntax or ExpressionStatementSyntax { Expression: ThrowExpressionSyntax })
				break;
		}

		return List(result);
	}

	/// <summary>
	/// Simplifies patterns like:
	/// - if (cond) { return true; } return false; => return cond;
	/// - if (cond) { return false; } return true; => return !cond;
	/// </summary>
	private static SyntaxList<StatementSyntax> SimplifyIfReturnPatterns(SyntaxList<StatementSyntax> statements)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();

		for (var i = 0; i < statements.Count; i++)
		{
			// Check for pattern: if (cond) { return <bool>; } followed by return <opposite bool>;
			if (i + 1 < statements.Count 
			    && statements[i] is IfStatementSyntax { Else: null } ifStatement 
			    && statements[i + 1] is ReturnStatementSyntax followingReturn 
			    && TryGetIfReturnBoolPattern(ifStatement, followingReturn, out var simplifiedReturn))
			{
				result.Add(simplifiedReturn!);
				i++; // Skip the following return statement
				continue;
			}

			result.Add(statements[i]);
		}

		return List(result);
	}

	/// <summary>
	/// Tries to simplify if-return-bool patterns.
	/// </summary>
	private static bool TryGetIfReturnBoolPattern(IfStatementSyntax ifStatement, ReturnStatementSyntax followingReturn, out ReturnStatementSyntax? simplified)
	{
		simplified = null;

		// Get the return statement from the if body
		var ifBody = ifStatement.Statement;
		ReturnStatementSyntax? ifReturn = ifBody switch
		{
			ReturnStatementSyntax ret => ret,
			BlockSyntax { Statements: [ ReturnStatementSyntax ret ] } => ret,
			_ => null
		};

		if (ifReturn is null)
		{
			return false;
		}

		// Check if both returns are boolean literals
		if (!TryGetBoolLiteral(ifReturn.Expression, out var ifReturnValue) 
		    || !TryGetBoolLiteral(followingReturn.Expression, out var followingReturnValue))
		{
			return false;
		}

		// Only simplify if they return opposite values
		if (ifReturnValue == followingReturnValue)
		{
			return false;
		}

		// if (cond) { return true; } return false; => return cond;
		// if (cond) { return false; } return true; => return !cond;
		var condition = ifStatement.Condition;

		if (ifReturnValue)
		{
			// return cond;
			simplified = ReturnStatement(condition);
		}
		else
		{
			// return !cond; (only add parentheses if needed)
			var negatedCondition = NeedsParenthesesForNegation(condition)
				? PrefixUnaryExpression(
					SyntaxKind.LogicalNotExpression,
					ParenthesizedExpression(condition))
				: PrefixUnaryExpression(
					SyntaxKind.LogicalNotExpression,
					condition);

			simplified = ReturnStatement(negatedCondition);
		}

		return true;
	}

	/// <summary>
	/// Determines if an expression needs parentheses when negated.
	/// </summary>
	private static bool NeedsParenthesesForNegation(ExpressionSyntax expression)
	{
		// These expression types don't need parentheses when negated
		return expression is not (IdentifierNameSyntax 
			or LiteralExpressionSyntax 
			or IsPatternExpressionSyntax 
			or InvocationExpressionSyntax 
			or MemberAccessExpressionSyntax 
			or ParenthesizedExpressionSyntax 
			or PrefixUnaryExpressionSyntax);
	}

	/// <summary>
	/// Tries to extract a boolean literal value from an expression.
	/// </summary>
	private static bool TryGetBoolLiteral(ExpressionSyntax? expression, out bool value)
	{
		value = false;

		if (expression is LiteralExpressionSyntax literal)
		{
			if (literal.IsKind(SyntaxKind.TrueLiteralExpression))
			{
				value = true;
				return true;
			}

			if (literal.IsKind(SyntaxKind.FalseLiteralExpression))
			{
				value = false;
				return true;
			}
		}

		return false;
	}

	/// <summary>
	/// Combines consecutive if statements that have identical bodies into a single if statement.
	/// Two strategies are applied in order:
	/// 1. Equality pattern: conditions of the form <c>x == literal</c> against the same variable
	///    are combined into <c>if (x is 1 or 5) { … }</c>.
	/// 2. General ||: when the body ends with a jump statement (return / break / continue / throw),
	///    any consecutive if statements with an identical body are combined using <c>||</c>.
	/// Example (strategy 1): if (1 == x) { return true; } if (5 == x) { return true; }
	///                     => if (x is 1 or 5) { return true; }
	/// Example (strategy 2): if (x &gt; 5) { return; } if (y &lt; 3) { return; }
	///                     => if (x &gt; 5 || y &lt; 3) { return; }
	/// </summary>
	internal static SyntaxList<StatementSyntax> CombineConsecutiveIfStatements(SyntaxList<StatementSyntax> statements)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();
		var i = 0;

		while (i < statements.Count)
		{
			// Only process if statements without an else clause
			if (statements[i] is not IfStatementSyntax currentIf || currentIf.Else is not null)
			{
				result.Add(statements[i]);
				i++;
				
				continue;
			}

			// Strategy 1: equality (is or) combination
			if (TryGetEqualityComparisonInfo(currentIf.Condition, out var targetIdentifier, out var firstLiteral))
			{
				var bodyString = currentIf.Statement.NormalizeWhitespace().ToFullString();
				var literals = new List<LiteralExpressionSyntax> { firstLiteral! };
				var j = i + 1;

				while (j < statements.Count)
				{
					if (statements[j] is not IfStatementSyntax { Else: null } nextIf)
					{
						break;
					}

					if (nextIf.Statement.NormalizeWhitespace().ToFullString() != bodyString)
					{
						break;
					}

					if (!TryGetEqualityComparisonInfo(nextIf.Condition, out var nextTarget, out var nextLiteral) ||
					    nextTarget != targetIdentifier)
					{
						break;
					}

					literals.Add(nextLiteral!);
					j++;
				}

				if (literals.Count > 1)
				{
					// e.g. target is 1 or 5 or 10
					var combinedCondition = CreateIsOrPattern(targetIdentifier!, literals);
					result.Add(currentIf.WithCondition(combinedCondition));
					i = j;
					continue;
				}
			}

			// Strategy 2: general || combination when body ends with a jump statement
			if (ContainsJumpStatement(currentIf.Statement))
			{
				var bodyString = currentIf.Statement.NormalizeWhitespace().ToFullString();
				var conditions = new List<ExpressionSyntax> { currentIf.Condition };
				var j = i + 1;

				while (j < statements.Count)
				{
					if (statements[j] is not IfStatementSyntax { Else: null } nextIf)
					{
						break;
					}

					if (nextIf.Statement.NormalizeWhitespace().ToFullString() != bodyString)
					{
						break;
					}

					conditions.Add(nextIf.Condition);
					j++;
				}

				if (conditions.Count > 1)
				{
					var combinedCondition = conditions[0];

					for (var k = 1; k < conditions.Count; k++)
					{
						combinedCondition = BinaryExpression(
							SyntaxKind.LogicalOrExpression,
							NeedsParenthesesInOrContext(combinedCondition)
								? ParenthesizedExpression(combinedCondition)
								: combinedCondition,
							NeedsParenthesesInOrContext(conditions[k])
								? ParenthesizedExpression(conditions[k])
								: conditions[k]);
					}

					result.Add(currentIf.WithCondition(combinedCondition));
					i = j;
					continue;
				}
			}

			result.Add(statements[i]);
			i++;
		}

		return List(result);
	}

	/// <summary>
	/// Returns <see langword="true"/> when a statement unconditionally ends with a jump
	/// (return, break, continue, or throw), making it safe to combine consecutive if
	/// statements with identical bodies using <c>||</c>.
	/// </summary>
	private static bool ContainsJumpStatement(StatementSyntax statement) =>
		statement switch
		{
			ReturnStatementSyntax => true,
			BreakStatementSyntax => true,
			ContinueStatementSyntax => true,
			ThrowStatementSyntax => true,
			BlockSyntax block => block.Statements.Count > 0 && ContainsJumpStatement(block.Statements.Last()),
			_ => false
		};

	/// <summary>
	/// Returns <see langword="true"/> when an expression needs parentheses when used as an
	/// operand of <c>||</c> (i.e., its precedence is lower than logical-or).
	/// </summary>
	private static bool NeedsParenthesesInOrContext(ExpressionSyntax expression) =>
		expression is ConditionalExpressionSyntax or AssignmentExpressionSyntax;

	/// <summary>
	/// Creates an 'is' pattern expression with 'or' for multiple values.
	/// Example: target is 1 or 5 or 10
	/// </summary>
	private static ExpressionSyntax CreateIsOrPattern(string targetIdentifier, List<LiteralExpressionSyntax> literals)
	{
		// Build pattern: 1 or 5 or 10 or ...
		PatternSyntax pattern = ConstantPattern(literals[0]);

		for (var k = 1; k < literals.Count; k++)
		{
			pattern = BinaryPattern(
				SyntaxKind.OrPattern,
				pattern,
				ConstantPattern(literals[k]));
		}

		// Create: target is <pattern>
		return IsPatternExpression(
			IdentifierName(targetIdentifier),
			pattern);
	}

	/// <summary>
	/// Tries to extract the comparison target identifier and literal value from an equality expression.
	/// Handles both 'value == target' and 'target == value' formats.
	/// </summary>
	private static bool TryGetEqualityComparisonInfo(ExpressionSyntax condition, out string? targetIdentifier, out LiteralExpressionSyntax? literal)
	{
		targetIdentifier = null;
		literal = null;

		if (condition is not BinaryExpressionSyntax binary 
		    || !binary.IsKind(SyntaxKind.EqualsExpression))
		{
			return false;
		}

		switch (binary)
		{
			// Check if left side is identifier and right side is literal
			case { Left: IdentifierNameSyntax leftId, Right: LiteralExpressionSyntax rightLit }:
				targetIdentifier = leftId.Identifier.Text;
				literal = rightLit;
				return true;
			// Check if right side is identifier and left side is literal
			case { Right: IdentifierNameSyntax rightId, Left: LiteralExpressionSyntax leftLit }:
				targetIdentifier = rightId.Identifier.Text;
				literal = leftLit;
				return true;
			default:
				return false;
		}

	}

	#region If-to-switch conversion

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
	private static bool TryConvertIfElseChainToSwitch(
		IfStatementSyntax node,
		[NotNullWhen(true)] out SyntaxNode? result)
	{
		result = null;
		string? switchTarget = null;
		var sections = new List<(List<ExpressionSyntax> Labels, StatementSyntax Body)>();

		// Walk the if / else-if chain.
		SyntaxNode? current = node;
		while (current is IfStatementSyntax ifNode)
		{
			if (!TryExtractSwitchLabels(ifNode.Condition, ref switchTarget, out var labels))
				return false;

			sections.Add((labels, ifNode.Statement));
			current = ifNode.Else?.Statement;
		}

		// Need at least 2 if-branches to be worth converting.
		if (sections.Count < 2)
			return false;

		if (switchTarget is null)
			return false;

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
				List([caseLabel]),
				List(BuildSwitchSectionStatements(body))));
		}

		if (defaultBody is not null)
		{
			switchSections.Add(SwitchSection(
				List<SwitchLabelSyntax>([DefaultSwitchLabel()]),
				List(BuildSwitchSectionStatements(defaultBody))));
		}

		result = SwitchStatement(IdentifierName(switchTarget), List(switchSections));
		return true;
	}

	/// <summary>
	/// Tries to build a <c>return x switch { ... };</c> statement.
	/// Succeeds only when every section (and the default body) contains a single
	/// <c>return expr;</c> statement.
	/// </summary>
	private static bool TryBuildSwitchExpression(
		string switchTarget,
		List<(List<ExpressionSyntax> Labels, StatementSyntax Body)> sections,
		StatementSyntax defaultBody,
		[NotNullWhen(true)] out StatementSyntax? result)
	{
		result = null;
		var arms = new List<SwitchExpressionArmSyntax>();

		foreach (var (labels, body) in sections)
		{
			if (!TryGetSingleReturnExpression(body, out var returnExpr))
				return false;

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
			return false;

		arms.Add(SwitchExpressionArm(DiscardPattern(), defaultExpr));

		result = ReturnStatement(
			SwitchExpression(
				IdentifierName(switchTarget),
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
			BlockSyntax { Statements: [ReturnStatementSyntax { Expression: { } expr }] }
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
		ref string? switchTarget,
		[NotNullWhen(true)] out List<ExpressionSyntax>? labels)
	{
		labels = null;
		var collected = new List<ExpressionSyntax>();

		if (!TryCollectLabels(condition, ref switchTarget, collected) || collected.Count == 0)
			return false;

		labels = collected;
		return true;
	}

	private static bool TryCollectLabels(
		ExpressionSyntax condition,
		ref string? switchTarget,
		List<ExpressionSyntax> labels)
	{
		// x == a || x == b
		if (condition is BinaryExpressionSyntax orExpr && orExpr.IsKind(SyntaxKind.LogicalOrExpression))
		{
			return TryCollectLabels(orExpr.Left, ref switchTarget, labels)
			       && TryCollectLabels(orExpr.Right, ref switchTarget, labels);
		}

		// x == constant  /  constant == x
		if (condition is BinaryExpressionSyntax eq && eq.IsKind(SyntaxKind.EqualsExpression))
		{
			string? target = null;
			ExpressionSyntax? label = null;

			if (eq.Left is IdentifierNameSyntax leftId && IsConstantLike(eq.Right))
			{
				target = leftId.Identifier.Text;
				label = eq.Right;
			}
			else if (eq.Right is IdentifierNameSyntax rightId && IsConstantLike(eq.Left))
			{
				target = rightId.Identifier.Text;
				label = eq.Left;
			}

			if (target is null || label is null)
				return false;

			if (switchTarget is not null && switchTarget != target)
				return false;

			switchTarget = target;
			labels.Add(label);
			return true;
		}

		// x is constant  /  x is (A or B)
		if (condition is IsPatternExpressionSyntax { Expression: IdentifierNameSyntax targetExpr } isExpr)
		{
			var target = targetExpr.Identifier.Text;

			if (switchTarget is not null && switchTarget != target)
				return false;

			if (!TryCollectPatternLabels(isExpr.Pattern, labels))
				return false;

			switchTarget = target;
			return true;
		}

		return false;
	}

	private static bool TryCollectPatternLabels(PatternSyntax pattern, List<ExpressionSyntax> labels)
	{
		switch (pattern)
		{
			case ConstantPatternSyntax constPat:
				labels.Add(constPat.Expression);
				return true;

			case BinaryPatternSyntax orPat when orPat.IsKind(SyntaxKind.OrPattern):
				return TryCollectPatternLabels(orPat.Left, labels)
				       && TryCollectPatternLabels(orPat.Right, labels);

			case ParenthesizedPatternSyntax paren:
				return TryCollectPatternLabels(paren.Pattern, labels);

			default:
				return false;
		}
	}

	/// <summary>
	/// Returns <see langword="true"/> when the expression is constant-like and therefore safe
	/// to use as a switch case label (literal, negative literal, enum member access).
	/// </summary>
	private static bool IsConstantLike(ExpressionSyntax expr) =>
		expr is LiteralExpressionSyntax
			or MemberAccessExpressionSyntax // e.g. Color.Red
			or PrefixUnaryExpressionSyntax { OperatorToken.RawKind: (int)SyntaxKind.MinusToken }; // -1

	/// <summary>
	/// Builds the statement list for a single switch section, automatically appending a
	/// <c>break;</c> when the body does not already end with a jump statement.
	/// </summary>
	private static List<StatementSyntax> BuildSwitchSectionStatements(StatementSyntax body)
	{
		var result = new List<StatementSyntax>();

		if (body is BlockSyntax block)
			result.AddRange(block.Statements);
		else
			result.Add(body);

		if (result.Count == 0 || !ContainsJumpStatement(result[result.Count - 1]))
			result.Add(BreakStatement());

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
			return CaseSwitchLabel(labels[0]);

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

	#endregion

	public override SyntaxNode? VisitReturnStatement(ReturnStatementSyntax node)
	{
		return node.WithExpression(Visit(node.Expression) as ExpressionSyntax);
	}

	public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
	{
		var visited = Visit(node.Declaration);

		return visited switch
		{
			null => null,
			BlockSyntax block => block,
			VariableDeclarationSyntax declaration => node.WithDeclaration(declaration),
			ExpressionStatementSyntax expressionStatement => expressionStatement,
			_ => node
		};
	}
}


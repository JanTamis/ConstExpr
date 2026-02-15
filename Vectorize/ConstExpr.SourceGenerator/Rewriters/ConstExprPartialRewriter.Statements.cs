using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static ConstExpr.SourceGenerator.Helpers.SyntaxHelpers;

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

		return node
			.WithCondition(condition as ExpressionSyntax ?? node.Condition)
			.WithStatement(statement as StatementSyntax ?? node.Statement)
			.WithElse(@else as ElseClauseSyntax);
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
					.ToList()!,
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

		return SyntaxFactory.List(result);
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

		return SyntaxFactory.List(result);
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
			simplified = SyntaxFactory.ReturnStatement(condition);
		}
		else
		{
			// return !cond; (only add parentheses if needed)
			var negatedCondition = NeedsParenthesesForNegation(condition)
				? SyntaxFactory.PrefixUnaryExpression(
					SyntaxKind.LogicalNotExpression,
					SyntaxFactory.ParenthesizedExpression(condition))
				: SyntaxFactory.PrefixUnaryExpression(
					SyntaxKind.LogicalNotExpression,
					condition);

			simplified = SyntaxFactory.ReturnStatement(negatedCondition);
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
	/// Combines consecutive if statements that have identical bodies and use equality comparisons
	/// against the same target variable into a single if statement with OR conditions.
	/// Example: if (1 == x) { return true; } if (5 == x) { return true; }
	///       => if (x is 1 or 5) { return true; }
	/// </summary>
	private SyntaxList<StatementSyntax> CombineConsecutiveIfStatements(SyntaxList<StatementSyntax> statements)
	{
		if (statements.Count < 2)
		{
			return statements;
		}

		var result = new List<StatementSyntax>();
		var i = 0;

		while (i < statements.Count)
		{
			// Check if current statement is an if statement that can be combined
			if (statements[i] is not IfStatementSyntax currentIf || currentIf.Else is not null)
			{
				result.Add(statements[i]);
				i++;
				
				continue;
			}

			// Try to extract the comparison target and literal value
			if (!TryGetEqualityComparisonInfo(currentIf.Condition, out var targetIdentifier, out var firstLiteral))
			{
				result.Add(statements[i]);
				i++;
				
				continue;
			}

			// Collect all consecutive if statements with the same body and target
			var bodyString = currentIf.Statement.NormalizeWhitespace().ToFullString();
			var literals = new List<LiteralExpressionSyntax> { firstLiteral! };
			var j = i + 1;

			while (j < statements.Count)
			{
				if (statements[j] is not IfStatementSyntax { Else: null } nextIf)
				{
					break;
				}

				// Check if the body matches
				var nextBodyString = nextIf.Statement.NormalizeWhitespace().ToFullString();
				
				if (bodyString != nextBodyString)
				{
					break;
				}

				// Check if the target matches and get the literal
				if (!TryGetEqualityComparisonInfo(nextIf.Condition, out var nextTarget, out var nextLiteral) ||
				    nextTarget != targetIdentifier)
				{
					break;
				}

				literals.Add(nextLiteral!);
				j++;
			}

			// If we found multiple if statements to combine
			if (literals.Count > 1)
			{
				// Create optimized condition using 'is' pattern with 'or'
				// e.g., target is 1 or 5 or 10
				var combinedCondition = CreateIsOrPattern(targetIdentifier!, literals);

				// Create combined if statement
				var combinedIf = currentIf.WithCondition(combinedCondition);
				result.Add(combinedIf);
				i = j;
			}
			else
			{
				result.Add(statements[i]);
				i++;
			}
		}

		return SyntaxFactory.List(result);
	}

	/// <summary>
	/// Creates an 'is' pattern expression with 'or' for multiple values.
	/// Example: target is 1 or 5 or 10
	/// </summary>
	private static ExpressionSyntax CreateIsOrPattern(string targetIdentifier, List<LiteralExpressionSyntax> literals)
	{
		// Build pattern: 1 or 5 or 10 or ...
		PatternSyntax pattern = SyntaxFactory.ConstantPattern(literals[0]);

		for (var k = 1; k < literals.Count; k++)
		{
			pattern = SyntaxFactory.BinaryPattern(
				SyntaxKind.OrPattern,
				pattern,
				SyntaxFactory.ConstantPattern(literals[k]));
		}

		// Create: target is <pattern>
		return SyntaxFactory.IsPatternExpression(
			SyntaxFactory.IdentifierName(targetIdentifier),
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


using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
					return TryUnrollForLoop(node, names);
			}
		}

		return VisitForStatementWithoutUnroll(node, names);
	}

	/// <summary>
	/// Tries to unroll a for loop when the condition is always true.
	/// </summary>
	private SyntaxNode? TryUnrollForLoop(ForStatementSyntax node, ImmutableHashSet<string> names)
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
				stringLiteral.Token.ValueText.Select(s => CreateLiteral(s) as CSharpSyntaxNode).ToList(),
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
	private void InvalidateAssignedVariables(SyntaxNode node)
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
		return node.WithStatements(VisitList(node.Statements));
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


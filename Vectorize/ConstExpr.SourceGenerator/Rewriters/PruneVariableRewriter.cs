using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;

namespace ConstExpr.SourceGenerator.Rewriters;

public sealed class PruneVariableRewriter(IDictionary<string, VariableItem> variables) : CSharpSyntaxRewriter
{
	public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node)
	{
		var items = node.Variables;
		var result = new List<VariableDeclaratorSyntax>();

		foreach (var item in items)
		{
			var visited = Visit(item);

			if (visited is VariableDeclaratorSyntax decl)
			{
				result.Add(decl);
			}
		}

		if (result.Count == 0)
		{
			return null;
		}

		return node.WithVariables(SyntaxFactory.SeparatedList(result));
	}

	public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
	{
		var identifier = node.Identifier.Text;

		if (variables.TryGetValue(identifier, out var value) && value.HasValue)
		{
			return null;
		}

		return base.VisitVariableDeclarator(node);
	}

	public override SyntaxNode? VisitBlock(BlockSyntax node)
	{
		var items = node.Statements;
		var result = new List<StatementSyntax>(items.Count);

		foreach (var item in items)
		{
			var visited = Visit(item);

			if (visited is StatementSyntax stmt)
			{
				result.Add(stmt);

				// Stop collecting further statements if this one terminates control flow
				if (IsTerminalStatement(stmt))
				{
					break;
				}
			}
		}

		return node.WithStatements(SyntaxFactory.List(result));
	}

	// Critical: if a variable declaration becomes empty after pruning, remove the whole local declaration statement
	public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
	{
		var visitedDeclaration = Visit(node.Declaration) as VariableDeclarationSyntax;

		// If the declaration is removed or contains no variables, drop the entire statement
		if (visitedDeclaration is null || visitedDeclaration.Variables.Count == 0)
		{
			return null;
		}

		return node.WithDeclaration(visitedDeclaration);
	}

	public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
	{
		var identifier = node.Left.ToString();

		if (variables.TryGetValue(identifier, out var value) && value.HasValue)
		{
			return null;
		}

		return base.VisitAssignmentExpression(node);
	}

	public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		var result = Visit(node.Expression);

		if (result is null)
		{
			return null;
		}

		return base.VisitExpressionStatement(node);
	}

	private static bool IsTerminalStatement(StatementSyntax statement)
	{
		// A statement that guarantees no following statement in the same block will execute
		return statement is ReturnStatementSyntax
			|| statement is ThrowStatementSyntax
			|| statement is BreakStatementSyntax
			|| statement is ContinueStatementSyntax
			|| (statement is YieldStatementSyntax ys && ys.Kind() == SyntaxKind.YieldBreakStatement);
	}
}

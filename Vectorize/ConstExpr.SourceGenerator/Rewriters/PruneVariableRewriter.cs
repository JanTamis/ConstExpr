using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

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
		var terminalReached = false;

		foreach (var item in items)
		{
			var visited = Visit(item);

			if (!terminalReached)
			{
				if (visited is StatementSyntax stmt)
				{
					result.Add(stmt);

					if (IsTerminalStatement(stmt))
					{
						terminalReached = true;
					}
				}
			}
			else
			{
				// Na een terminale statement: blijf wel visit-en maar neem alleen lokale functies op
				if (visited is LocalFunctionStatementSyntax localFunc)
				{
					result.Add(localFunc);
				}
			}
		}

		return node.WithStatements(SyntaxFactory.List(result));
	}

	// Critical: if a variable declaration becomes empty after pruning, remove the whole local declaration statement
	public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
	{
		// If the declaration is removed or contains no variables, drop the entire statement
		if (Visit(node.Declaration) is not VariableDeclarationSyntax visitedDeclaration || visitedDeclaration.Variables.Count == 0)
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

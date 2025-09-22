using ConstExpr.SourceGenerator.Visitors;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Helpers;

namespace ConstExpr.SourceGenerator.Rewriters;

public sealed class PruneVariableRewriter(SemanticModel semanticModel, MetadataLoader loader, IDictionary<string, VariableItem> variables) : BaseRewriter(semanticModel, loader, variables)
{
	public override SyntaxNode? Visit(SyntaxNode? node)
	{
		try
		{
			return base.Visit(node);
		}
		catch (Exception e)
		{
			return node;
		}
	}

	public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
	{
		if (variables.TryGetValue(node.Identifier.Text, out var value) && value.HasValue)
		{
			return null;
		}

		return base.VisitIdentifierName(node);
	}

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

	public override SyntaxNode? VisitCheckedStatement(CheckedStatementSyntax node)
	{
		var visited = VisitBlock(node.Block);

		if (visited is BlockSyntax { Statements.Count: > 0 } block)
		{
			return node.WithBlock(block);
		}

		return null;
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
				if (visited is StatementSyntax statementSyntax)
				{
					result.Add(statementSyntax);
				}
				else if (visited is ExpressionStatementSyntax { Expression: null } expressionStatement)
				{
					result.Add(SyntaxFactory.ExpressionStatement(expressionStatement.Expression));
				}
				else
				{
					result.Add(SyntaxFactory.ExpressionStatement(visited as ExpressionSyntax));
				}

				if (IsTerminalStatement(visited))
				{
					terminalReached = true;
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

		if (variables.TryGetValue(identifier, out var value) && value.HasValue && TryGetLiteralValue(node.Right, out _))
		{
			return null;
		}

		return node;
	}

	public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		var result = Visit(node.Expression);

		return result;
	}

	// New override: strip all comment trivia (including XML doc comments) from tokens
	public override SyntaxToken VisitToken(SyntaxToken token)
	{
		if (token.IsKind(SyntaxKind.None))
		{
			return token;
		}

		var leading = FilterTrivia(token.LeadingTrivia);
		var trailing = FilterTrivia(token.TrailingTrivia);

		// Ensure space after 'return' keyword if an expression follows immediately (no whitespace/comment/newline)
		if (token.IsKind(SyntaxKind.ReturnKeyword) && token.Parent is ReturnStatementSyntax { Expression: not null })
		{
			var hasWhitespace = trailing.Any(t => t.IsKind(SyntaxKind.WhitespaceTrivia));
			var hasNewLine = trailing.Any(t => t.IsKind(SyntaxKind.EndOfLineTrivia));
			
			if (!hasWhitespace && !hasNewLine)
			{
				trailing = trailing.Add(SyntaxFactory.Space);
			}
		}
		
		if (leading != token.LeadingTrivia || trailing != token.TrailingTrivia)
		{
			token = token.WithLeadingTrivia(leading).WithTrailingTrivia(trailing);
		}

		return base.VisitToken(token);
	}

	private static SyntaxTriviaList FilterTrivia(SyntaxTriviaList triviaList)
	{
		return SyntaxFactory.TriviaList(GetFiltered(triviaList));

		static IEnumerable<SyntaxTrivia> GetFiltered(SyntaxTriviaList triviaList)
		{
			foreach (var t in triviaList)
			{
				switch (t.Kind())
				{
					case SyntaxKind.SingleLineCommentTrivia:
					case SyntaxKind.MultiLineCommentTrivia:
					case SyntaxKind.SingleLineDocumentationCommentTrivia:
					case SyntaxKind.MultiLineDocumentationCommentTrivia:
						continue; // skip comment
				}

				yield return t;
			}
		}
	}

	private static bool IsTerminalStatement(SyntaxNode statement)
	{
		// A statement that guarantees no following statement in the same block will execute
		return statement is ReturnStatementSyntax
			|| statement is ThrowStatementSyntax
			|| statement is BreakStatementSyntax
			|| statement is ContinueStatementSyntax
			|| (statement is YieldStatementSyntax ys && ys.IsKind(SyntaxKind.YieldBreakStatement));
	}
}

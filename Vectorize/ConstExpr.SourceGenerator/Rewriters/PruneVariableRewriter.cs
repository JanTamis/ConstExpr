using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

public sealed class PruneVariableRewriter(
	SemanticModel semanticModel, 
	MetadataLoader loader, 
	IDictionary<string, VariableItem> variables,
	ConcurrentDictionary<string, ISymbol> symbolStore,
	RoslynApiCache? apiCache = null, 
	CancellationToken cancellationToken = default)
	: BaseRewriter(semanticModel, loader, variables, symbolStore)
{
	public override SyntaxNode? Visit(SyntaxNode? node)
	{
		try
		{
			return base.Visit(node);
		}
		catch (Exception)
		{
			return node;
		}
	}

	#region Statement Pruning

	public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
	{
		// If the declaration is removed or contains no variables, drop the entire statement.
		if (Visit(node.Declaration) is not VariableDeclarationSyntax visitedDeclaration || visitedDeclaration.Variables.Count == 0)
		{
			return null;
		}

		return node.WithDeclaration(visitedDeclaration);
	}

	public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node)
	{
		var remaining = node.Variables
			.Where(v => !CanBePruned(v.Identifier.Text))
			.ToList();

		if (remaining.Count == 0)
		{
			return null;
		}

		return node.WithVariables(SeparatedList(remaining));
	}

	public override SyntaxNode? VisitVariableDeclarator(VariableDeclaratorSyntax node)
	{
		return CanBePruned(node.Identifier.Text) ? null : base.VisitVariableDeclarator(node);
	}

	public override SyntaxNode VisitBlock(BlockSyntax node)
	{
		var statements = new List<StatementSyntax>(node.Statements.Count);
		var terminalReached = false;

		foreach (var statement in node.Statements)
		{
			var visited = Visit(statement);

			if (terminalReached)
			{
				// Keep local functions even after a terminal statement.
				if (visited is LocalFunctionStatementSyntax localFunc)
				{
					statements.Add(localFunc);
				}
				continue;
			}

			if (visited is not StatementSyntax stmt)
			{
				continue;
			}

			statements.Add(stmt);
			terminalReached = IsTerminalStatement(stmt);
		}

		return node.WithStatements(List(statements));
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

	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		var statement = Visit(node.Statement);
		var elseClause = node.Else is not null ? Visit(node.Else) as ElseClauseSyntax : null;

		// Body is empty — either remove the whole if, or keep only the else body.
		if (statement is null or BlockSyntax { Statements.Count: 0 })
		{
			return elseClause is null ? null : elseClause.Statement;
		}

		var result = node.WithStatement(statement as StatementSyntax ?? node.Statement);

		if (elseClause is not null)
		{
			result = result.WithElse(elseClause);
		}
		else if (node.Else is not null)
		{
			result = result.WithElse(null);
		}

		return result;
	}

	public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		var result = Visit(node.Expression);

		if (result is null)
		{
			return null;
		}

		return result is ExpressionSyntax expr ? node.WithExpression(expr) : node;
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		// If iterating over a pruned variable, remove the foreach entirely.
		if (node.Expression is IdentifierNameSyntax id && CanBePruned(id.Identifier.Text))
		{
			return null;
		}

		return base.VisitForEachStatement(node);
	}

	#endregion

	#region Expression Pruning

	public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
	{
		return CanBePruned(node.Identifier.Text) ? null : base.VisitIdentifierName(node);
	}

	public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
	{
		if (ShouldPruneAssignment(node))
		{
			return null;
		}

		return base.VisitAssignmentExpression(node);
	}

	public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		// Prune instance calls on a constant receiver (e.g. x.Foo() where x is a known constant).
		if (node.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax receiver }
		    && CanBePruned(receiver.Identifier.Text))
		{
			return null;
		}

		return base.VisitInvocationExpression(node);
	}

	public override SyntaxNode? VisitConditionalExpression(ConditionalExpressionSyntax node)
	{
		// Fold a ternary whose condition is a known constant.
		if (TryGetLiteralValue(node.Condition, out var conditionValue) && conditionValue is bool boolValue)
		{
			return Visit(boolValue ? node.WhenTrue : node.WhenFalse);
		}

		return base.VisitConditionalExpression(node);
	}

	public override SyntaxNode? VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
	{
		return CanBePruned(node.Operand) ? null : base.VisitPostfixUnaryExpression(node);
	}

	public override SyntaxNode? VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
	{
		return CanBePruned(node.Operand) ? null : base.VisitPrefixUnaryExpression(node);
	}

	#endregion

	#region Helpers

	private bool ShouldPruneAssignment(AssignmentExpressionSyntax assignment)
	{
		// Self-assignment: x = x
		if (assignment.Left.IsEquivalentTo(assignment.Right))
		{
			return true;
		}

		switch (assignment.Left)
		{
			// Direct assignment to a prunable variable.
			case IdentifierNameSyntax id when CanBePruned(id.Identifier.Text):
				return true;

			// Tuple deconstruction where every element is prunable: (a, b) = (1, 2)
			case TupleExpressionSyntax tuple
				when assignment.IsKind(SyntaxKind.SimpleAssignmentExpression):
			{
				if (tuple.Arguments.All(arg =>
				        arg.Expression is IdentifierNameSyntax tupleId && CanBePruned(tupleId.Identifier.Text)))
				{
					return true;
				}
				break;
			}
		}

		return false;
	}

	private static bool IsTerminalStatement(SyntaxNode? statement)
	{
		return statement is ReturnStatementSyntax
			or ThrowStatementSyntax
			or BreakStatementSyntax
			or ContinueStatementSyntax
			or YieldStatementSyntax { RawKind: (int) SyntaxKind.YieldBreakStatement };
	}

	#endregion

	#region Trivia Cleanup

	public override SyntaxToken VisitToken(SyntaxToken token)
	{
		if (token.IsKind(SyntaxKind.None))
		{
			return token;
		}

		var leading = FilterCommentTrivia(token.LeadingTrivia);
		var trailing = FilterCommentTrivia(token.TrailingTrivia);

		// Ensure at least one space after 'return' when an expression follows directly.
		if (token.IsKind(SyntaxKind.ReturnKeyword) && token.Parent is ReturnStatementSyntax { Expression: not null })
		{
			if (!trailing.Any(t => t.IsKind(SyntaxKind.WhitespaceTrivia) || t.IsKind(SyntaxKind.EndOfLineTrivia)))
			{
				trailing = trailing.Add(Space);
			}
		}

		if (leading != token.LeadingTrivia || trailing != token.TrailingTrivia)
		{
			return token.WithLeadingTrivia(leading).WithTrailingTrivia(trailing);
		}

		return base.VisitToken(token);
	}

	private static SyntaxTriviaList FilterCommentTrivia(SyntaxTriviaList triviaList)
	{
		var filtered = triviaList.Where(t => t.Kind() switch
		{
			SyntaxKind.SingleLineCommentTrivia => false,
			SyntaxKind.MultiLineCommentTrivia => false,
			SyntaxKind.SingleLineDocumentationCommentTrivia => false,
			SyntaxKind.MultiLineDocumentationCommentTrivia => false,
			_ => true
		});

		return TriviaList(filtered);
	}

	#endregion
}
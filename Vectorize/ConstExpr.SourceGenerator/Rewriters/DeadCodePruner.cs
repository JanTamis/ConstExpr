using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
/// Simplified dead code pruner using the Mark-and-Sweep pattern.
/// First collects all variable usages, then prunes in a single rewrite pass.
/// </summary>
public sealed class DeadCodePruner(VariableUsageCollector usageCollector, IDictionary<string, VariableItem> variables, SemanticModel model) : CSharpSyntaxRewriter
{
	/// <summary>
	/// Prunes dead code from a syntax node using the Mark-and-Sweep pattern.
	/// </summary>
	public static SyntaxNode Prune(SyntaxNode node, IDictionary<string, VariableItem> variables, SemanticModel model)
	{
		// Phase 1: Mark - collect all variable usages
		var collector = new VariableUsageCollector(variables.Keys);
		collector.Visit(node);

		// Phase 2: Sweep - rewrite and prune dead code
		var pruner = new DeadCodePruner(collector, variables, model);
		return pruner.Visit(node);
	}

	/// <summary>
	/// Determines if a variable can be pruned based on collected usage data and variable state.
	/// </summary>
	private bool CanBePruned(string variableName)
	{
		// Must not be read anywhere
		if (!usageCollector.CanBePruned(variableName))
		{
			return false;
		}

		// If variable is not tracked (e.g., local in nested scope), it can be pruned if not read
		if (!variables.TryGetValue(variableName, out var variable))
		{
			// Variable not in our tracking dictionary - if it's not read, it can be pruned
			return true;
		}

		// For tracked variables, must have a constant value and not be altered
		return variable.HasValue && !variable.IsAltered;
	}

	#region Statement Pruning

	public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
	{
		if (Visit(node.Declaration) is not VariableDeclarationSyntax declaration || declaration.Variables.Count == 0)
		{
			return null;
		}

		return node.WithDeclaration(declaration);
	}

	public override SyntaxNode? VisitVariableDeclaration(VariableDeclarationSyntax node)
	{
		var remainingVariables = node.Variables
			.Where(v => !CanBePruned(v.Identifier.Text))
			.ToList();

		switch (remainingVariables.Count)
		{
			case 0:
				return null;
			case 1:
				node = node.WithType(SyntaxFactory.ParseTypeName("var"));
				break;
		}

		return node.WithVariables(SyntaxFactory.SeparatedList(remainingVariables));
	}

	public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		switch (node.Expression)
		{
			// Prune assignments to dead variables
			case AssignmentExpressionSyntax assignment when ShouldPruneAssignment(assignment):
			// Prune increment/decrement on dead variables
			case PostfixUnaryExpressionSyntax { Operand: IdentifierNameSyntax postfixId }
				when CanBePruned(postfixId.Identifier.Text):
			case PrefixUnaryExpressionSyntax { Operand: IdentifierNameSyntax prefixId }
				when CanBePruned(prefixId.Identifier.Text):
				return null;
			default:
			{
				var visited = Visit(node.Expression);

				return visited is ExpressionSyntax expr ? node.WithExpression(expr) : node;
			}
		}
	}

	public override SyntaxNode? VisitBlock(BlockSyntax node)
	{
		var statements = new List<StatementSyntax>();
		var terminalReached = false;

		foreach (var statement in node.Statements)
		{
			var visited = Visit(statement);

			switch (terminalReached)
			{
				case false when visited is StatementSyntax stmt:
				{
					statements.Add(stmt);

					if (IsTerminalStatement(stmt))
					{
						terminalReached = true;
					}
					break;
				}
				case true when visited is LocalFunctionStatementSyntax localFunc:
					// Keep local functions even after terminal statements
					statements.Add(localFunc);
					break;
			}
		}

		return node.WithStatements(SyntaxFactory.List(statements));
	}

	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		var statement = Visit(node.Statement);
		var elseClause = node.Else is not null ? Visit(node.Else) as ElseClauseSyntax : null;

		// If the body is empty and there's no else, remove the entire if
		if (statement is null or BlockSyntax { Statements.Count: 0 })
		{
			if (elseClause is null)
			{
				return null;
			}

			// Just the else remains - return its body
			return elseClause.Statement;
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

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		// If iterating over a pruned variable, remove the foreach
		if (node.Expression is IdentifierNameSyntax id && CanBePruned(id.Identifier.Text))
		{
			return null;
		}

		return base.VisitForEachStatement(node);
	}

	#endregion

	#region Expression Pruning

	public override SyntaxNode? VisitAssignmentExpression(AssignmentExpressionSyntax node)
	{
		if (ShouldPruneAssignment(node))
		{
			return null;
		}
		
		var right = Visit(node.Right);

		if (right is null)
		{
			return null;
		}

		return node.WithRight(right as ExpressionSyntax ?? node.Right);
	}

	public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		// If calling a method on a pruned variable, prune the call
		if (node.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax id }
		    && CanBePruned(id.Identifier.Text)
		    && model.Compilation.GetTypeByMetadataName($"System.{id}") is null)
		{
			return null;
		}

		return base.VisitInvocationExpression(node);
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
			// Assignment to prunable variable
			case IdentifierNameSyntax id when CanBePruned(id.Identifier.Text):
			{
				return true;
			}
			// Tuple assignment where all elements are prunable
			case TupleExpressionSyntax tuple:
			{
				var allPrunable = tuple.Arguments.All(arg =>
					arg.Expression is IdentifierNameSyntax tupleId && CanBePruned(tupleId.Identifier.Text));

				if (allPrunable)
				{
					return true;
				}
				break;
			}
		}

		return false;
	}

	private static bool IsTerminalStatement(StatementSyntax statement)
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

		// Ensure space after 'return' keyword
		if (token.IsKind(SyntaxKind.ReturnKeyword) && token.Parent is ReturnStatementSyntax { Expression: not null })
		{
			if (!trailing.Any(t => t.IsKind(SyntaxKind.WhitespaceTrivia, SyntaxKind.EndOfLineTrivia)))
			{
				trailing = trailing.Add(SyntaxFactory.Space);
			}
		}

		if (leading != token.LeadingTrivia || trailing != token.TrailingTrivia)
		{
			return token.WithLeadingTrivia(leading).WithTrailingTrivia(trailing);
		}

		return token;
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

		return SyntaxFactory.TriviaList(filtered);
	}

	#endregion
}
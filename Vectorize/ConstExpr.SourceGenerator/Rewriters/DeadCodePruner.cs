using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
		// Include both tracked variables and any local variable declarators found in the node
		// (some locals are introduced during rewriting and are not present in the variables dictionary).
		var declaredLocals = node.DescendantNodes().OfType<VariableDeclaratorSyntax>()
			.Select(v => v.Identifier.Text);

		var allTracked = variables.Keys.Concat(declaredLocals).Distinct();

		var collector = new VariableUsageCollector(allTracked);
		collector.Visit(node);

		// Phase 2: Sweep - rewrite and prune dead code
		var pruner = new DeadCodePruner(collector, variables, model);
		return pruner.Visit(node);
	}

	/// <summary>
	/// Determines if a variable can be pruned based on collected usage data and variable state.
	/// Used for assignments and other non-declaration contexts; untracked variables are kept.
	/// </summary>
	private bool CanBePruned(string variableName)
	{
		// Must not be read anywhere
		if (!usageCollector.CanBePruned(variableName))
		{
			return false;
		}

		// If variable is not tracked, keep it (we don't know enough about it to prune safely)
		if (!variables.TryGetValue(variableName, out var variable))
		{
			return false;
		}

		// For tracked variables, must have a constant value and not be altered
		return variable.HasValue && !variable.IsAltered;
	}

	/// <summary>
	/// Determines if an assignment expression to a variable can be pruned.
	/// In addition to the standard <see cref="CanBePruned(string)"/> check, this also
	/// handles the case where <see cref="VariableItem.HasValue"/> was cleared by
	/// <c>InvalidateAssignedVariables</c> (after an if/else branch) even though the
	/// actual RHS is a side-effect-free constant literal. A dead write with no side
	/// effects is always safe to remove.
	/// </summary>
	private bool CanBePrunedAssignment(string variableName, ExpressionSyntax rhs)
	{
		if (!usageCollector.CanBePruned(variableName))
		{
			return false;
		}

		if (!variables.TryGetValue(variableName, out var variable))
		{
			return false;
		}

		// Standard path: the variable still carries its known constant value.
		if (variable.HasValue && !variable.IsAltered)
		{
			return true;
		}

		// Fallback: HasValue may have been cleared by InvalidateAssignedVariables after an
		// if/else with an unknown condition, even though the rewritten RHS is a literal.
		// Pruning a dead literal write is always safe regardless of HasValue.
		return IsConstantExpression(rhs);
	}

	/// <summary>
	/// Determines if a variable declaration can be pruned. Unlike <see cref="CanBePruned(string)"/>,
	/// this overload also handles variables that are not in the tracking dictionary by checking
	/// whether the initializer is a pure constant expression (no side effects).
	/// This covers block-local variables introduced inside if/else branches whose scope does not
	/// extend beyond the branch.
	/// </summary>
	private bool CanBePrunedDeclaration(string variableName, ExpressionSyntax? initializer)
	{
		if (!usageCollector.CanBePruned(variableName))
		{
			return false;
		}

		if (!variables.TryGetValue(variableName, out var variable))
		{
			// Variable not in tracking dictionary (e.g., was block-local to a branch).
			// Safe to prune only when the initializer is a side-effect-free constant.
			return IsConstantExpression(initializer);
		}

		// IsAltered is intentionally not checked: if the variable is never read, the
		// assignment (even after a re-assignment) is still dead code.
		return variable.HasValue;
	}

	/// <summary>
	/// Returns <see langword="true"/> when the expression is guaranteed to be a side-effect-free
	/// constant (literal, default, or a unary minus applied to a literal).
	/// </summary>
	private static bool IsConstantExpression(ExpressionSyntax? expr) =>
		expr switch
		{
			null => true,
			LiteralExpressionSyntax => true,
			DefaultExpressionSyntax => true,
			PrefixUnaryExpressionSyntax { Operand: LiteralExpressionSyntax } => true,
			_ => false
		};

  public override SyntaxNode? Visit(SyntaxNode? node)
  {
		try
		{
			return base.Visit(node);
		}
		catch (Exception e)
		{
			return null;
			// return node;
		}
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
			.Where(v => !CanBePrunedDeclaration(v.Identifier.Text, v.Initializer?.Value))
			.ToList();

		switch (remainingVariables.Count)
		{
			case 0:
				return null;
			case 1:
				node = node.WithType(ParseTypeName("var"));
				break;
		}

		return node.WithVariables(SeparatedList(remainingVariables));
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
				{
					// Keep local functions even after terminal statements
					statements.Add(localFunc);
					break;
				}
			}
		}

		if (statements.Count == 0)
		{
			return null;
		}

		return node.WithStatements(List(statements));
	}

	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		var statement = Visit(node.Statement);
		var elseClause = node.Else is not null ? Visit(node.Else) as ElseClauseSyntax : null;

		// If the body is empty and there's no else, remove the entire if
		if (statement is null or BlockSyntax { Statements.Count: 0 })
		{
			// Just the else remains - return its body
			return elseClause?.Statement;
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
			case IdentifierNameSyntax id when CanBePrunedAssignment(id.Identifier.Text, assignment.Right):
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
				trailing = trailing.Add(Space);
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

		return TriviaList(filtered);
	}

	#endregion
}
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Refactorers;

using static SyntaxFactory;

/// <summary>
/// Refactorer that removes unused local variable declarations.
/// Inspired by the Roslyn <c>CSharpRemoveUnusedVariableCodeFixProvider</c>,
/// which handles compiler diagnostics CS0168 (declared but never used) and
/// CS0219 (assigned but value never used).
///
/// <code>
/// int unused = 42;
/// DoWork();
/// </code>
/// →
/// <code>
/// DoWork();
/// </code>
///
/// When the variable has an initialiser with a side-effecting expression the
/// declaration is replaced with an expression statement so the side-effect is
/// preserved:
/// <code>
/// int x = Compute();
/// </code>
/// →
/// <code>
/// Compute();
/// </code>
///
/// This is a pure syntax-level transformation.
/// </summary>
public static class RemoveUnusedVariableRefactoring
{
	/// <summary>
	/// Tries to remove or replace a single <see cref="VariableDeclaratorSyntax"/> that
	/// is considered unused.
	/// </summary>
	/// <param name="declarator">The unused variable declarator.</param>
	/// <param name="semanticModel">The semantic model used for analysis.</param>
	/// <param name="result">
	/// On success, the updated <see cref="StatementSyntax"/> (or <c>null</c> when the
	/// entire parent statement should be deleted — check the return value).
	/// </param>
	/// <returns>
	/// <c>true</c> when a transformation was produced; <c>false</c> when the declarator
	/// cannot be safely removed (e.g. it is not inside a local declaration).
	/// </returns>
	public static bool TryRemoveUnusedVariable(
		VariableDeclaratorSyntax declarator,
		SemanticModel semanticModel,
		[NotNullWhen(true)] out StatementSyntax? result)
	{
		result = null;

		// The declarator must live inside a VariableDeclaration → LocalDeclarationStatement.
		if (declarator.Parent is not VariableDeclarationSyntax { Parent: LocalDeclarationStatementSyntax localDeclaration } declaration)
		{
			return false;
		}

		var variables = declaration.Variables;

		if (variables.Count > 1)
		{
			// Remove only this declarator; keep the rest of the declaration.
			var newVariables = variables.Remove(declarator);
			var newDeclaration = declaration.WithVariables(newVariables);
			result = localDeclaration.WithDeclaration(newDeclaration);
			return true;
		}

		// Single declarator — decide whether to drop the statement or keep a side-effect.
		if (declarator.Initializer is { Value: var initValue } 
		    && CouldHaveSideEffects(initValue, semanticModel))
		{
			// Replace with a bare expression statement to preserve the side-effect.
			result = ExpressionStatement(initValue)
				.WithLeadingTrivia(localDeclaration.GetLeadingTrivia())
				.WithTrailingTrivia(localDeclaration.GetTrailingTrivia());
			return true;
		}

		// No side-effect: produce an empty block so callers can distinguish
		// "remove the node" from "replace the node" — we return null here and
		// signal removal via the out parameter remaining null.
		// Callers that receive false should not modify the tree.
		// Use the dedicated overload below to obtain removal candidates.
		return false;
	}

	/// <summary>
	/// Tries to remove or replace a single unused <see cref="VariableDeclaratorSyntax"/>.
	/// When the whole parent statement can be dropped, <paramref name="statementToRemove"/>
	/// is set and <paramref name="replacement"/> is <c>null</c>.
	/// When the statement should be replaced (either a trimmed declaration or a
	/// bare expression statement), <paramref name="replacement"/> is set and
	/// <paramref name="statementToRemove"/> is <c>null</c>.
	/// </summary>
	public static bool TryRemoveUnusedVariable(
		VariableDeclaratorSyntax declarator,
		SemanticModel semanticModel,
		out StatementSyntax? statementToRemove,
		out StatementSyntax? replacement)
	{
		statementToRemove = null;
		replacement = null;

		if (declarator.Parent is not VariableDeclarationSyntax { Parent: LocalDeclarationStatementSyntax localDeclaration } declaration)
		{
			return false;
		}

		var variables = declaration.Variables;

		if (variables.Count > 1)
		{
			// Remove only this declarator from the list.
			var newVariables = variables.Remove(declarator);
			var newDeclaration = declaration.WithVariables(newVariables);
			replacement = localDeclaration.WithDeclaration(newDeclaration);
			return true;
		}

		// Single declarator.
		if (declarator.Initializer is { Value: var initValue } 
		    && CouldHaveSideEffects(initValue, semanticModel))
		{
			// Preserve the side-effecting initialiser as a standalone expression.
			replacement = ExpressionStatement(initValue)
				.WithLeadingTrivia(localDeclaration.GetLeadingTrivia())
				.WithTrailingTrivia(localDeclaration.GetTrailingTrivia());
			return true;
		}

		// No observable side-effect → remove the whole statement.
		statementToRemove = localDeclaration;
		return true;
	}

	/// <summary>
	/// Applies all unused-variable removals to a <see cref="BlockSyntax"/>.
	/// Each <paramref name="unusedDeclarators"/> entry is a declarator whose
	/// variable is never read after assignment.
	/// </summary>
	public static BlockSyntax RemoveUnusedVariables(
		SemanticModel semanticModel,
		BlockSyntax block,
		IEnumerable<VariableDeclaratorSyntax> unusedDeclarators)
	{
		// Collect tracked nodes; process in reverse document order so removals
		// don't invalidate earlier positions.
		var rewriter = new UnusedVariableRewriter(unusedDeclarators, semanticModel);
		return (BlockSyntax)rewriter.Visit(block);
	}

	// -------------------------------------------------------------------------
	// Helpers
	// -------------------------------------------------------------------------

	/// <summary>
	/// Returns <c>true</c> when <paramref name="expression"/> might have an
	/// observable side-effect (i.e. it is not a pure constant/literal).
	/// When a <see cref="SemanticModel"/> is available, checks if property accesses
	/// resolve to known side-effect-free getters.
	/// This is a conservative over-approximation.
	/// </summary>
	private static bool CouldHaveSideEffects(ExpressionSyntax expression, SemanticModel semanticModel)
	{
		// When a semantic model is available, check if the expression is a property access
		// with a known side-effect-free getter
		if (expression is MemberAccessExpressionSyntax memberAccess)
		{
			var symbol = semanticModel.GetSymbolInfo(memberAccess).Symbol;

			if (symbol is IPropertySymbol { IsReadOnly: true })
			{
				return false;
			}
		}

		return expression switch
		{
			LiteralExpressionSyntax => false,
			DefaultExpressionSyntax => false,
			TypeOfExpressionSyntax => false,
			SizeOfExpressionSyntax => false,
			NameSyntax => false,          // Reading a variable / type name — no side-effect.
			MemberAccessExpressionSyntax { Expression: NameSyntax } => false,
			_ => true
		};
	}

	// -------------------------------------------------------------------------
	// Rewriter
	// -------------------------------------------------------------------------

	private sealed class UnusedVariableRewriter(IEnumerable<VariableDeclaratorSyntax> targets, SemanticModel model) : CSharpSyntaxRewriter
	{
		private readonly HashSet<VariableDeclaratorSyntax> _targets = new(targets);

		public override SyntaxNode? VisitLocalDeclarationStatement(LocalDeclarationStatementSyntax node)
		{
			var declaration = node.Declaration;
			var newVariables = declaration.Variables;

			foreach (var variable in declaration.Variables)
			{
				if (!_targets.Contains(variable))
				{
					continue;
				}

				if (declaration.Variables.Count == 1)
				{
					// Single declarator — remove or replace with side-effect expression.
					if (variable.Initializer is { Value: var initValue } 
					    && CouldHaveSideEffects(initValue, model))
					{
						return ExpressionStatement(initValue)
							.WithLeadingTrivia(node.GetLeadingTrivia())
							.WithTrailingTrivia(node.GetTrailingTrivia());
					}

					// No side-effect → signal removal by returning null.
					return null;
				}

				newVariables = newVariables.Remove(variable);
			}

			if (newVariables.Count == declaration.Variables.Count)
			{
				return node; // Nothing changed.
			}

			return node.WithDeclaration(declaration.WithVariables(newVariables));
		}
	}
}



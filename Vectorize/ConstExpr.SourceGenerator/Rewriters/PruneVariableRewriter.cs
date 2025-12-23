using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ConstExpr.SourceGenerator.Rewriters;

public sealed class PruneVariableRewriter(SemanticModel semanticModel, MetadataLoader loader, IDictionary<string, VariableItem> variables, RoslynApiCache? apiCache = null, CancellationToken cancellationToken = default)
	: BaseRewriter(semanticModel, loader, variables)
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

	public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
	{
		if (CanBePruned(node.Identifier.Text))
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

			if (visited is VariableDeclaratorSyntax decl && !CanBePruned(decl.Identifier.Text))
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

		if (CanBePruned(identifier))
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
				switch (visited)
				{
					case ExpressionStatementSyntax expressionStatement:
						result.Add(SyntaxFactory.ExpressionStatement(expressionStatement.Expression));
						break;
					case StatementSyntax statementSyntax:
						result.Add(statementSyntax);
						break;
					case ExpressionSyntax expressionSyntax:
						result.Add(SyntaxFactory.ExpressionStatement(expressionSyntax));
						break;
				}

				if (visited is StatementSyntax stmt && IsTerminalStatement(stmt))
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
		if (node.Left.IsEquivalentTo(node.Right))
		{
			return null;
		}

		// Handle Tuple deconstruction assignments: (a, b) = (1, 2)
		if (node.Left is TupleExpressionSyntax leftTuple && node.OperatorToken.IsKind(SyntaxKind.EqualsToken))
		{
			var allElementsHaveValue = true;

			// Check if all tuple elements have constant values
			foreach (var arg in leftTuple.Arguments)
			{
				if (arg.Expression is IdentifierNameSyntax { Identifier.Text: var name })
				{
					if (!variables.TryGetValue(name, out var tupleValue) || !tupleValue.HasValue)
					{
						allElementsHaveValue = false;
						break;
					}
				}
				else
				{
					allElementsHaveValue = false;
					break;
				}
			}

			// If all tuple elements have constant values, the assignment can be pruned
			if (allElementsHaveValue)
			{
				return null;
			}

			// Otherwise keep the assignment
			return node;
		}

		var identifier = node.Left.ToString();

		if (CanBePruned(identifier))
		{
			return null;
		}

		return node;
	}

	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		var body = Visit(node.Statement);

		if (body is null or BlockSyntax { Statements.Count: 0 } && node.Else is null)
		{
			return null;
		}

		return base.VisitIfStatement(node);
	}

	public override SyntaxNode? VisitExpressionStatement(ExpressionStatementSyntax node)
	{
		var result = Visit(node.Expression);

		// If the expression was pruned to null, remove the entire statement
		if (result is null)
		{
			return null;
		}

		// If the result is the same expression, return the original statement
		if (ReferenceEquals(result, node.Expression))
		{
			return node;
		}

		// If we have a new expression, create a new statement with it
		if (result is ExpressionSyntax expression)
		{
			return node.WithExpression(expression);
		}

		return result;
	}

	public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
	{
		if (node.Expression is MemberAccessExpressionSyntax { Expression: IdentifierNameSyntax identifier }
		    && CanBePruned(identifier.Identifier.Text)
		    || node.ArgumentList.Arguments.Any(a => CanBePruned(a.Expression)))
		{
			return null;
		}

		return base.VisitInvocationExpression(node);
	}

	public override SyntaxNode? VisitMemberAccessExpression(MemberAccessExpressionSyntax node)
	{
		// If accessing a member on a constant value that doesn't change state, it can potentially be pruned
		if (TryGetLiteralValue(node.Expression, out _))
		{
			// Check if this member access is for a property or field that doesn't have side effects
			var symbolInfo = apiCache?.GetOrAddSymbolInfo(node, semanticModel, cancellationToken) ?? semanticModel.GetSymbolInfo(node, cancellationToken);

			switch (symbolInfo.Symbol)
			{
				case IPropertySymbol { IsReadOnly: true }:
					// This is accessing a readonly property on a constant - safe to prune if not used
					return node; // Let parent visitor decide if it should be pruned
				case IFieldSymbol { IsReadOnly: true }:
					// This is accessing a readonly field on a constant - safe to prune if not used
					return node; // Let parent visitor decide if it should be pruned
			}

		}

		return base.VisitMemberAccessExpression(node);
	}

	public override SyntaxNode? VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
	{
		// Check if this is creating an object with constant arguments that has no side effects
		var symbolInfo = apiCache?.GetOrAddSymbolInfo(node, semanticModel, cancellationToken) ?? semanticModel.GetSymbolInfo(node, cancellationToken);

		if (symbolInfo.Symbol is IMethodSymbol { ContainingType.IsValueType: true })
		{
			// Check if it's a value type or immutable type that's safe to prune
			// For value types, check if all constructor arguments are constants
			var allArgsConstant = true;

			if (node.ArgumentList != null)
			{
				foreach (var arg in node.ArgumentList.Arguments)
				{
					if (!TryGetLiteralValue(arg.Expression, out _))
					{
						allArgsConstant = false;
						break;
					}
				}
			}

			// If all arguments are constant, this object creation can be pruned if not used
			if (allArgsConstant)
			{
				return node; // Let parent visitor decide if it should be pruned
			}
		}

		return base.VisitObjectCreationExpression(node);
	}

	public override SyntaxNode? VisitElementAccessExpression(ElementAccessExpressionSyntax node)
	{
		// Check if we're accessing an element of a constant collection with constant indices
		if (TryGetLiteralValue(node.Expression, out _))
		{
			var allIndicesConstant = true;

			foreach (var arg in node.ArgumentList.Arguments)
			{
				if (!TryGetLiteralValue(arg.Expression, out _))
				{
					allIndicesConstant = false;
					break;
				}
			}

			// If both collection and indices are constant, this can potentially be pruned
			if (allIndicesConstant)
			{
				return node; // Let parent visitor decide if it should be pruned
			}
		}

		return base.VisitElementAccessExpression(node);
	}

	public override SyntaxNode? VisitConditionalExpression(ConditionalExpressionSyntax node)
	{
		// If the condition is a constant, we can prune the unused branch
		if (TryGetLiteralValue(node.Condition, out var conditionValue))
		{
			if (conditionValue is bool boolValue)
			{
				// Return the appropriate branch and let it be further processed
				return Visit(boolValue ? node.WhenTrue : node.WhenFalse);
			}
		}

		return base.VisitConditionalExpression(node);
	}

	public override SyntaxNode? VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
	{
		// Handle i++, i--, etc. on constant variables
		if (CanBePruned(node.Operand))
		{
			return null;
		}

		return base.VisitPostfixUnaryExpression(node);
	}

	public override SyntaxNode? VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
	{
		// Handle ++i, --i, etc. on constant variables
		if (CanBePruned(node.Operand))
		{
			return null;
		}

		return base.VisitPrefixUnaryExpression(node);
	}

	// Strip all comment trivia (including XML doc comments) from tokens
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

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		var list = Visit(node.Expression);

		if (list is null)
		{
			return null;
		}

		return base.VisitForEachStatement(node);
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

	private static bool IsTerminalStatement(SyntaxNode? statement)
	{
		// A statement that guarantees no following statement in the same block will execute
		return statement is ReturnStatementSyntax
		       or ThrowStatementSyntax
		       or BreakStatementSyntax
		       or ContinueStatementSyntax
		       or YieldStatementSyntax { RawKind: (int)SyntaxKind.YieldBreakStatement };
	}

	private static bool HasPureAttribute(IMethodSymbol method)
	{
		// Check for [Pure] attribute from System.Diagnostics.Contracts
		return method.GetAttributes().Any(attr =>
			attr.AttributeClass?.ToDisplayString() == "System.Diagnostics.Contracts.PureAttribute");
	}
}
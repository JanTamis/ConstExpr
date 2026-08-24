using System.Collections.Generic;
using ConstExpr.SourceGenerator.Comparers;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Helpers;

public static class IdentifierAliasResolver
{
	public static bool AreEqual(SyntaxNode left, SyntaxNode right, IDictionary<string, VariableItem> variables)
	{
		if (SyntaxNodeComparer.Get().Equals(left, right))
		{
			return true;
		}

		if (left is not IdentifierNameSyntax leftIdentifier || right is not IdentifierNameSyntax rightIdentifier)
		{
			return false;
		}

		if (leftIdentifier.Identifier.Text == rightIdentifier.Identifier.Text)
		{
			return true;
		}

		if (variables.TryGetValue(leftIdentifier.Identifier.Text, out var leftVar)
		    && variables.TryGetValue(rightIdentifier.Identifier.Text, out var rightVar)
		    && leftVar.Value is ArgumentSyntax leftArgument
		    && rightVar.Value is ArgumentSyntax rightArgument
		    && SyntaxNodeComparer.Get().Equals(leftArgument.Expression, rightArgument.Expression))
		{
			return true;
		}

		var leftRoot = ResolveAlias(leftIdentifier, variables);
		var rightRoot = ResolveAlias(rightIdentifier, variables);

		return SyntaxNodeComparer.Get().Equals(leftRoot, rightRoot);
	}

	/// <summary>
	///   Follows a chain of `var y = x;` aliases (stored as <see cref="VariableItem.Value" /> being an
	///   <see cref="IdentifierNameSyntax" />) back to its root identifier, so two names that both refer to the
	///   same underlying value are recognized as equal even though neither is foldable to a constant.
	///   Cycle-safe via a visited-set walk.
	/// </summary>
	private static SyntaxNode ResolveAlias(IdentifierNameSyntax identifier, IDictionary<string, VariableItem> variables)
	{
		var visited = new HashSet<string>();
		SyntaxNode current = identifier;

		while (current is IdentifierNameSyntax id && visited.Add(id.Identifier.Text))
		{
			if (!variables.TryGetValue(id.Identifier.Text, out var variable) || variable.Value is not IdentifierNameSyntax alias)
			{
				break;
			}

			current = alias;
		}

		return current;
	}
}
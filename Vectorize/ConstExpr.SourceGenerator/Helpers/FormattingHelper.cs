using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Rewriters;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Helpers;

internal static class FormattingHelper
{
	public static SyntaxNode Format(SyntaxNode node)
	{
		// NormalizeWhitespace corrupts structured trivia (e.g. XML doc comments lose their text).
		// Strip the leading trivia before normalizing, then restore it afterwards.
		var leadingTrivia = node.GetLeadingTrivia();
		var rewriter = new BlockFormattingRewriter();
		var result = rewriter.Visit(node.WithoutLeadingTrivia().NormalizeWhitespace("\t"))!;

		if (leadingTrivia.Count > 0)
		{
			result = result.WithLeadingTrivia(leadingTrivia);
		}

		return result;
	}

	public static string? Render([NotNullIfNotNull(nameof(node))] SyntaxNode? node)
	{
		if (node is null)
		{
			return null;
		}

		var formatted = Format(node);

		// SyntaxNode.ToString() excludes the leading trivia of the first token, so XML doc
		// comments would be lost. Use ToFullString() when there is leading trivia to preserve.
		return formatted.GetLeadingTrivia().Count > 0
			? formatted.ToFullString()
			: formatted.ToString();
	}
}

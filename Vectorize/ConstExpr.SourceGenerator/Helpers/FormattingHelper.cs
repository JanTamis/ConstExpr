using ConstExpr.SourceGenerator.Rewriters;
using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Helpers;

internal static class FormattingHelper
{
	public static SyntaxNode Format(SyntaxNode node)
	{
		var rewriter = new BlockFormattingRewriter();

		return rewriter.Visit(node.NormalizeWhitespace("\t"));
	}
	
	public static string? Render(SyntaxNode? node)
	{
		if (node is null)
		{
			return null;
		}

		return Format(node).ToString();
	}
}

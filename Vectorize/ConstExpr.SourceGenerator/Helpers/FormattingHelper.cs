using Microsoft.CodeAnalysis;

namespace ConstExpr.SourceGenerator.Helpers;

internal static partial class FormattingHelper
{
	public static SyntaxNode Format(SyntaxNode node)
	{
		var rewriter = new BlockFormattingRewriter();

		return rewriter.Visit(node.NormalizeWhitespace("\t"));
	}
	
	public static string Render(SyntaxNode node)
	{
		return Format(node).ToFullString();
	}
}

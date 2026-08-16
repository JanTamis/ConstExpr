using System.Diagnostics.CodeAnalysis;
using ConstExpr.SourceGenerator.Rewriters;
using Microsoft.CodeAnalysis;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Helpers;

internal static class FormattingHelper
{
	/// <summary>
	///   Renders generated code with the full pipeline: shape passes, whitespace normalization and
	///   brace placement.
	/// </summary>
	/// <remarks>
	///   <para>The order is load-bearing:</para>
	///   <list type="number">
	///     <item>
	///       <see cref="BracePreferenceRewriter" /> and <see cref="ExpressionBodyRewriter" /> create
	///       new nodes, which carry no trivia, so they must run <em>before</em> normalization.
	///     </item>
	///     <item><c>NormalizeWhitespace</c> lays everything out with the configured indent and EOL.</item>
	///     <item>
	///       <see cref="BlockFormattingRewriter" /> counts end-of-line trivia for its blank-line
	///       logic and therefore only works <em>after</em> normalization.
	///     </item>
	///     <item><see cref="BracePlacementRewriter" /> is trivia-only and runs last.</item>
	///   </list>
	///   <para>
	///     Under <see cref="FormattingOptions.Default" /> phases 1, 2 and 5 are inert, so the
	///     historic <c>NormalizeWhitespace</c> -> <see cref="BlockFormattingRewriter" /> path is all
	///     that runs and the output is unchanged.
	///   </para>
	/// </remarks>
	public static SyntaxNode Format(SyntaxNode node, FormattingOptions? options = null)
	{
		var resolved = options ?? FormattingOptions.Default;

		// NormalizeWhitespace corrupts structured trivia (e.g. XML doc comments lose their text).
		// Strip the leading trivia before normalizing, then restore it afterwards.
		var leadingTrivia = node.GetLeadingTrivia();

		var shaped = BracePreferenceRewriter.Apply(node.WithoutLeadingTrivia(), resolved);
		shaped = ExpressionBodyRewriter.Apply(shaped, resolved);

		var normalized = shaped.NormalizeWhitespace(resolved.IndentationString, resolved.EndOfLine);
		var result = new BlockFormattingRewriter(resolved).Visit(normalized);

		result = BracePlacementRewriter.Apply(result, resolved);
		result = SpacingRewriter.Apply(result, resolved);
		result = IndentationRewriter.Apply(result, resolved);

		if (leadingTrivia.Count > 0)
		{
			result = result.WithLeadingTrivia(leadingTrivia);
		}

		return result;
	}

	/// <summary>
	///   Lays out hand-written code without the shape passes: only whitespace normalization and
	///   brace placement.
	/// </summary>
	/// <remarks>
	///   Used for the generator's own source templates. <see cref="BlockFormattingRewriter" /> is
	///   deliberately skipped there: it strips comments, rewrites literals into scientific notation
	///   and folds compound assignments, none of which should be applied to code that was written
	///   by hand.
	/// </remarks>
	public static SyntaxNode Layout(SyntaxNode node, FormattingOptions? options = null)
	{
		var resolved = options ?? FormattingOptions.Default;

		var result = BracePlacementRewriter.Apply(node.NormalizeWhitespace(resolved.IndentationString, resolved.EndOfLine), resolved);

		result = SpacingRewriter.Apply(result, resolved);

		return IndentationRewriter.Apply(result, resolved);
	}

	public static string? Render([NotNullIfNotNull(nameof(node))] SyntaxNode? node, FormattingOptions? options = null)
	{
		if (node is null)
		{
			return null;
		}

		return ToText(Format(node, options));
	}

	/// <summary>
	///   <see cref="Layout" /> followed by rendering to text.
	/// </summary>
	public static string? RenderLayout([NotNullIfNotNull(nameof(node))] SyntaxNode? node, FormattingOptions? options = null)
	{
		if (node is null)
		{
			return null;
		}

		return ToText(Layout(node, options));
	}

	private static string ToText(SyntaxNode formatted)
	{
		// SyntaxNode.ToString() excludes the leading trivia of the first token, so XML doc
		// comments would be lost. Use ToFullString() when there is leading trivia to preserve.
		return formatted.GetLeadingTrivia().Count > 0
			? formatted.ToFullString()
			: formatted.ToString();
	}
}
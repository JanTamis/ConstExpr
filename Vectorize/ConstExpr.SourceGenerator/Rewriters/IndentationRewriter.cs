using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Rewriters;

using static SyntaxFactory;

/// <summary>
///   Applies the <c>csharp_indent_*</c> options, plus
///   <c>csharp_preserve_single_line_blocks</c>, to an already normalised tree.
/// </summary>
/// <remarks>
///   <para>
///     <c>NormalizeWhitespace</c> indents with one fixed policy, so these options are expressed as
///     a <em>shift</em> applied afterwards: for each construct whose option deviates, every token
///     that starts a line inside that construct has its leading whitespace moved in or out by one
///     level. Shifts accumulate, so a token inside both a switch section and a de-indented block
///     ends up moved by the sum.
///   </para>
///   <para>
///     Under <see cref="FormattingOptions.Default" /> nothing deviates and the pass returns the
///     tree untouched.
///   </para>
/// </remarks>
public static class IndentationRewriter
{
	/// <summary>
	///   Re-indents the constructs whose options deviate, or returns <paramref name="node" />
	///   untouched when they are all at their default.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node, FormattingOptions options)
	{
		var result = options.PreserveSingleLineBlocks
			? node
			: ExpandSingleLineAccessorLists(node, options);

		// FlushLeft is deliberately absent here: it names an absolute column, which a member rendered
		// on its own cannot know because it is embedded inside a type later. It is applied to the
		// finished file instead.
		if (options is { IndentCaseContents: true, IndentCaseContentsWhenBlock: true, IndentSwitchLabels: true, IndentBlockContents: true, IndentBraces: false } and ({ IndentLabels: LabelIndentation.NoChange } or { IndentLabels: LabelIndentation.FlushLeft }))
		{
			return result;
		}

		// A line's indentation can be split across two trivia lists - the previous token's trailing
		// and this token's leading - because earlier passes append line breaks on either side. Both
		// halves are therefore read and rewritten as a pair, exactly like the other layout passes.
		var tokens = result.DescendantTokens().ToList();
		var lineStarts = new Dictionary<SyntaxToken, string>();

		for (var i = 1; i < tokens.Count; i++)
		{
			if (TryGetIndentation(tokens[i - 1], tokens[i], out var indentation))
			{
				lineStarts[tokens[i]] = indentation;
			}
		}

		var targets = ComputeTargets(result, options, lineStarts);

		if (targets.Count == 0)
		{
			return result;
		}

		// Rewriting a token's indentation also means clearing the half that lives on the token
		// before it, so both members of every affected pair are replaced.
		var replacements = new Dictionary<SyntaxToken, SyntaxToken>();

		for (var i = 1; i < tokens.Count; i++)
		{
			var token = tokens[i];

			if (!targets.TryGetValue(token, out var target))
			{
				continue;
			}

			replacements[tokens[i - 1]] = tokens[i - 1].WithTrailingTrivia(StripTrailingIndentation(tokens[i - 1].TrailingTrivia));
			replacements[token] = token.WithLeadingTrivia(WithIndentation(token.LeadingTrivia, target));
		}

		return result.ReplaceTokens(replacements.Keys, (original, _) => replacements[original]);
	}

	/// <summary>
	///   The new indentation for every line-starting token whose indentation actually changes.
	/// </summary>
	private static Dictionary<SyntaxToken, string> ComputeTargets(SyntaxNode root, FormattingOptions options, Dictionary<SyntaxToken, string> lineStarts)
	{
		// A token can sit inside several deviating constructs at once, so the shifts are summed
		// before anything is rewritten rather than applied construct by construct.
		var shifts = new Dictionary<SyntaxToken, int>();

		CollectShifts(root, options, lineStarts, shifts);

		var targets = new Dictionary<SyntaxToken, string>();

		foreach (var pair in shifts)
		{
			var current = lineStarts[pair.Key];
			var target = Shift(current, pair.Value, options);

			if (!String.Equals(target, current, StringComparison.Ordinal))
			{
				targets[pair.Key] = target;
			}
		}

		return targets;
	}

	/// <summary>
	///   Whether <paramref name="token" /> is the first on its line, and if so what the line's
	///   indentation currently is. The indentation is every whitespace character after the last line
	///   break, wherever that whitespace happens to be stored.
	/// </summary>
	private static bool TryGetIndentation(SyntaxToken previous, SyntaxToken token, out string indentation)
	{
		indentation = String.Empty;

		var leading = token.LeadingTrivia;
		var trailing = previous.TrailingTrivia;

		var inLeading = LastLineBreak(leading);

		if (inLeading >= 0)
		{
			indentation = WhitespaceAfter(leading, inLeading);

			return true;
		}

		var inTrailing = LastLineBreak(trailing);

		if (inTrailing < 0)
		{
			return false;
		}

		// The break is on the previous token, so the indentation is whatever whitespace follows it
		// there plus everything on this token.
		indentation = WhitespaceAfter(trailing, inTrailing) + WhitespaceAfter(leading, -1);

		return true;
	}

	private static int LastLineBreak(SyntaxTriviaList trivia)
	{
		for (var i = trivia.Count - 1; i >= 0; i--)
		{
			if (trivia[i].IsKind(SyntaxKind.EndOfLineTrivia))
			{
				return i;
			}
		}

		return -1;
	}

	/// <summary>
	///   The whitespace that follows <paramref name="index" /> in the list, or an empty string when
	///   anything other than whitespace follows.
	/// </summary>
	private static string WhitespaceAfter(SyntaxTriviaList trivia, int index)
	{
		var builder = StringBuilderCache.Acquire(8);

		for (var i = index + 1; i < trivia.Count; i++)
		{
			if (!trivia[i].IsKind(SyntaxKind.WhitespaceTrivia))
			{
				StringBuilderCache.GetStringAndRelease(builder);

				return String.Empty;
			}

			builder.Append(trivia[i].ToFullString());
		}

		return StringBuilderCache.GetStringAndRelease(builder);
	}

	private static string Shift(string current, int levels, FormattingOptions options)
	{
		if (levels == 0)
		{
			return current;
		}

		var unit = options.IndentationString;

		if (levels > 0)
		{
			return current + Repeat(unit, levels);
		}

		for (var i = 0; i < -levels && current.EndsWith(unit, StringComparison.Ordinal); i++)
		{
			current = current.Substring(0, current.Length - unit.Length);
		}

		return current;
	}

	/// <summary>
	///   Removes the indentation half that sits after the last line break of a trailing trivia list.
	/// </summary>
	private static SyntaxTriviaList StripTrailingIndentation(SyntaxTriviaList trivia)
	{
		var lastBreak = LastLineBreak(trivia);

		if (lastBreak < 0 || lastBreak == trivia.Count - 1)
		{
			return trivia;
		}

		var kept = new List<SyntaxTrivia>(trivia.Count);

		for (var i = 0; i <= lastBreak; i++)
		{
			kept.Add(trivia[i]);
		}

		for (var i = lastBreak + 1; i < trivia.Count; i++)
		{
			if (!trivia[i].IsKind(SyntaxKind.WhitespaceTrivia))
			{
				kept.Add(trivia[i]);
			}
		}

		return TriviaList(kept);
	}

	/// <summary>
	///   Replaces the indentation of a leading trivia list, keeping everything up to and including
	///   its last line break.
	/// </summary>
	private static SyntaxTriviaList WithIndentation(SyntaxTriviaList trivia, string indentation)
	{
		var lastBreak = LastLineBreak(trivia);
		var kept = new List<SyntaxTrivia>(trivia.Count + 1);

		for (var i = 0; i <= lastBreak; i++)
		{
			kept.Add(trivia[i]);
		}

		for (var i = lastBreak + 1; i < trivia.Count; i++)
		{
			if (!trivia[i].IsKind(SyntaxKind.WhitespaceTrivia))
			{
				kept.Add(trivia[i]);
			}
		}

		if (indentation.Length > 0)
		{
			kept.Add(Whitespace(indentation));
		}

		return TriviaList(kept);
	}

	private static void CollectShifts(SyntaxNode root, FormattingOptions options, Dictionary<SyntaxToken, string> lineStarts, Dictionary<SyntaxToken, int> shifts)
	{
		foreach (var descendant in root.DescendantNodes())
		{
			switch (descendant)
			{
				case SwitchSectionSyntax section:
				{
					CollectSwitchSectionShifts(section, options, lineStarts, shifts);

					break;
				}
				case BlockSyntax block:
				{
					CollectBlockShifts(block, options, lineStarts, shifts);

					break;
				}
				case LabeledStatementSyntax labeled when options.IndentLabels == LabelIndentation.OneLessThanCurrent:
				{
					AddShift(lineStarts, shifts, labeled.Identifier, -1);

					break;
				}
			}
		}
	}

	private static void CollectSwitchSectionShifts(SwitchSectionSyntax section, FormattingOptions options, Dictionary<SyntaxToken, string> lineStarts, Dictionary<SyntaxToken, int> shifts)
	{
		if (!options.IndentSwitchLabels)
		{
			foreach (var label in section.Labels)
			{
				AddShift(lineStarts, shifts, label, -1);
			}
		}

		// csharp_indent_case_contents_when_block only governs a case whose body is a single block;
		// every other body shape is governed by csharp_indent_case_contents.
		var indented = section.Statements is [ BlockSyntax ]
			? options.IndentCaseContentsWhenBlock
			: options.IndentCaseContents;

		if (indented)
		{
			return;
		}

		foreach (var statement in section.Statements)
		{
			AddShift(lineStarts, shifts, statement, -1);
		}
	}

	private static void CollectBlockShifts(BlockSyntax block, FormattingOptions options, Dictionary<SyntaxToken, string> lineStarts, Dictionary<SyntaxToken, int> shifts)
	{
		if (!options.IndentBlockContents)
		{
			foreach (var statement in block.Statements)
			{
				AddShift(lineStarts, shifts, statement, -1);
			}
		}

		if (!options.IndentBraces)
		{
			return;
		}

		AddShift(lineStarts, shifts, block.OpenBraceToken, 1);
		AddShift(lineStarts, shifts, block.CloseBraceToken, 1);
	}

	/// <summary>
	///   The label tokens that <see cref="LabelIndentation.FlushLeft" /> moves to column 0, which is
	///   an absolute position rather than a shift.
	/// </summary>
	private static void AddShift(Dictionary<SyntaxToken, string> lineStarts, Dictionary<SyntaxToken, int> shifts, SyntaxNode node, int levels)
	{
		foreach (var token in node.DescendantTokens())
		{
			AddShift(lineStarts, shifts, token, levels);
		}
	}

	private static void AddShift(Dictionary<SyntaxToken, string> lineStarts, Dictionary<SyntaxToken, int> shifts, SyntaxToken token, int levels)
	{
		if (!lineStarts.ContainsKey(token))
		{
			return;
		}

		shifts[token] = shifts.TryGetValue(token, out var existing)
			? existing + levels
			: levels;
	}

	/// <summary>
	///   Breaks <c>{ get; set; }</c> style accessor lists across lines for
	///   <c>csharp_preserve_single_line_blocks = false</c>.
	/// </summary>
	private static SyntaxNode ExpandSingleLineAccessorLists(SyntaxNode node, FormattingOptions options)
	{
		var lists = node
			.DescendantNodes()
			.OfType<AccessorListSyntax>()
			.Where(list => !list.ToFullString().Contains('\n'))
			.ToList();

		if (lists.Count == 0)
		{
			return node;
		}

		var newLine = ElasticEndOfLine(options.EndOfLine);

		return node.ReplaceNodes(lists, (original, rewritten) =>
		{
			// The accessor list opens on the declaration's line, so the line's indentation is the
			// one carried by the member the list belongs to.
			var leading = original.Parent?.GetFirstToken().LeadingTrivia ?? default;
			var indentation = WhitespaceAfter(leading, LastLineBreak(leading));

			return rewritten
				.WithOpenBraceToken(rewritten.OpenBraceToken.WithTrailingTrivia(newLine))
				.WithAccessors(List(rewritten.Accessors.Select(accessor => accessor
					.WithLeadingTrivia(Whitespace(indentation + options.IndentationString))
					.WithTrailingTrivia(newLine))))
				.WithCloseBraceToken(rewritten.CloseBraceToken.WithLeadingTrivia(Whitespace(indentation)));
		});
	}

	private static string Repeat(string value, int count)
	{
		switch (count)
		{
			case <= 0:
			{
				return String.Empty;
			}
			case 1:
			{
				return value;
			}
		}

		var builder = StringBuilderCache.Acquire(value.Length * count);

		for (var i = 0; i < count; i++)
		{
			builder.Append(value);
		}

		return StringBuilderCache.GetStringAndRelease(builder);
	}
}
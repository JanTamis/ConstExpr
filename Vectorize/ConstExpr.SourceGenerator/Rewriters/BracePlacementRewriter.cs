using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Rewriters;

using static SyntaxFactory;

/// <summary>
///   Moves opening braces (and <c>else</c>/<c>catch</c>/<c>finally</c> keywords) onto the previous
///   line for the constructs that <c>csharp_new_line_before_open_brace</c> excludes.
/// </summary>
/// <remarks>
///   <para>
///     Pure trivia surgery on an already normalised tree; it never changes the shape of the syntax.
///     <c>NormalizeWhitespace</c> always emits Allman style, so only the "join to the previous line"
///     direction is ever needed.
///   </para>
///   <para>
///     The line break lives in the <em>trailing</em> trivia of the preceding token while the
///     indentation lives in the <em>leading</em> trivia of the brace, so both have to be edited
///     together. Working from the flat token list and a single
///     <see cref="SyntaxNodeExtensions.ReplaceTokens{TRoot}" /> keeps that pairing simple and
///     handles every construct uniformly.
///   </para>
///   <para>
///     Under <see cref="FormattingOptions.Default" /> (<c>all</c>, and every
///     <c>NewLineBefore*</c> true) this pass touches nothing.
///   </para>
/// </remarks>
public static class BracePlacementRewriter
{
	/// <summary>
	///   Joins braces and keywords to the previous line where the options ask for it, or returns
	///   <paramref name="node" /> untouched when every construct keeps its line break.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node, FormattingOptions options)
	{
		if (options is { NewLineBeforeOpenBrace: BraceNewLinePlacement.All, NewLineBeforeElse: true, NewLineBeforeCatch: true, NewLineBeforeFinally: true, NewLineBeforeMembersInObjectInitializers: true, NewLineBeforeMembersInAnonymousTypes: true, NewLineBetweenQueryExpressionClauses: true })
		{
			return node;
		}

		var tokens = node.DescendantTokens().ToList();
		var replacements = new Dictionary<SyntaxToken, SyntaxToken>();

		for (var i = 1; i < tokens.Count; i++)
		{
			var token = tokens[i];
			var previous = tokens[i - 1];

			if (!CanJoin(previous, token, options))
			{
				continue;
			}

			replacements[previous] = previous.WithTrailingTrivia(TriviaList(Space));
			replacements[token] = token.WithLeadingTrivia(TriviaList());
		}

		return replacements.Count == 0
			? node
			: node.ReplaceTokens(replacements.Keys, (original, _) => replacements[original]);
	}

	/// <summary>
	///   Whether <paramref name="token" /> wants to move up and the trivia between the two tokens
	///   can be collapsed without losing anything.
	/// </summary>
	private static bool CanJoin(SyntaxToken previous, SyntaxToken token, FormattingOptions options)
	{
		if (!ShouldJoinToPreviousLine(token, options))
		{
			return false;
		}

		// Comments or directives between the two tokens carry meaning that a join would destroy.
		if (!IsOnlyLayoutTrivia(previous.TrailingTrivia) || !IsOnlyLayoutTrivia(token.LeadingTrivia))
		{
			return false;
		}

		// A token that is already on the previous line needs no work.
		return previous.TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia) || token.LeadingTrivia.Any(SyntaxKind.EndOfLineTrivia);
	}

	/// <summary>
	///   Whether the trivia list consists purely of layout (line breaks and indentation), so that
	///   collapsing it loses nothing.
	/// </summary>
	private static bool IsOnlyLayoutTrivia(SyntaxTriviaList trivia)
	{
		foreach (var item in trivia)
		{
			if (!item.IsKind(SyntaxKind.WhitespaceTrivia) && !item.IsKind(SyntaxKind.EndOfLineTrivia))
			{
				return false;
			}
		}

		return true;
	}

	private static bool ShouldJoinToPreviousLine(SyntaxToken token, FormattingOptions options)
	{
		return token.RawKind switch
		{
			(int) SyntaxKind.OpenBraceToken => GetPlacement(token) is { } placement && !options.NewLineBeforeOpenBrace.HasFlag(placement),
			(int) SyntaxKind.ElseKeyword => !options.NewLineBeforeElse && token.Parent is ElseClauseSyntax,
			(int) SyntaxKind.CatchKeyword => !options.NewLineBeforeCatch && token.Parent is CatchClauseSyntax,
			(int) SyntaxKind.FinallyKeyword => !options.NewLineBeforeFinally && token.Parent is FinallyClauseSyntax,
			_ => StartsCollapsedMember(token, options)
		};
	}

	/// <summary>
	///   Whether the token opens a member of an initializer, an anonymous type or a query
	///   expression whose members are configured to share one line.
	/// </summary>
	/// <remarks>
	///   Only the "pull onto one line" direction is handled, mirroring how the brace options work:
	///   <c>NormalizeWhitespace</c> already gives each of these members its own line, so the
	///   <see langword="true" /> value needs no work.
	/// </remarks>
	private static bool StartsCollapsedMember(SyntaxToken token, FormattingOptions options)
	{
		var parent = token.Parent;

		while (parent is not null)
		{
			// Only the first token of the member itself counts; a token deeper inside it is on the
			// member's own line and is not what the option governs.
			if (parent.GetFirstToken() != token)
			{
				return false;
			}

			switch (parent.Parent)
			{
				case InitializerExpressionSyntax initializer:
				{
					return initializer.IsKind(SyntaxKind.ObjectInitializerExpression)
						? !options.NewLineBeforeMembersInObjectInitializers
						: !options.NewLineBeforeMembersInAnonymousTypes && initializer.Parent is AnonymousObjectCreationExpressionSyntax;
				}
				case AnonymousObjectCreationExpressionSyntax:
				{
					return !options.NewLineBeforeMembersInAnonymousTypes;
				}
				case QueryBodySyntax or QueryExpressionSyntax:
				{
					return !options.NewLineBetweenQueryExpressionClauses;
				}
			}

			parent = parent.Parent;
		}

		return false;
	}

	/// <summary>
	///   Maps an opening brace to the <c>csharp_new_line_before_open_brace</c> category it belongs
	///   to, or <see langword="null" /> when the construct has no category (namespaces have no
	///   setting, so their braces are always left alone).
	/// </summary>
	private static BraceNewLinePlacement? GetPlacement(SyntaxToken token)
	{
		return token.Parent switch
		{
			BlockSyntax block => GetBlockPlacement(block),
			TypeDeclarationSyntax or EnumDeclarationSyntax => BraceNewLinePlacement.Types,
			AccessorListSyntax accessorList => accessorList.Parent switch
			{
				PropertyDeclarationSyntax => BraceNewLinePlacement.Properties,
				IndexerDeclarationSyntax => BraceNewLinePlacement.Indexers,
				EventDeclarationSyntax => BraceNewLinePlacement.Events,
				_ => null
			},
			SwitchStatementSyntax => BraceNewLinePlacement.ControlBlocks,
			InitializerExpressionSyntax => BraceNewLinePlacement.ObjectCollectionArrayInitializers,
			AnonymousObjectCreationExpressionSyntax => BraceNewLinePlacement.AnonymousTypes,
			_ => null
		};
	}

	private static BraceNewLinePlacement? GetBlockPlacement(BlockSyntax block)
	{
		return block.Parent switch
		{
			MethodDeclarationSyntax or ConstructorDeclarationSyntax or DestructorDeclarationSyntax
				or OperatorDeclarationSyntax or ConversionOperatorDeclarationSyntax => BraceNewLinePlacement.Methods,
			LocalFunctionStatementSyntax => BraceNewLinePlacement.LocalFunctions,
			AccessorDeclarationSyntax => BraceNewLinePlacement.Accessors,
			AnonymousMethodExpressionSyntax => BraceNewLinePlacement.AnonymousMethods,
			SimpleLambdaExpressionSyntax or ParenthesizedLambdaExpressionSyntax => BraceNewLinePlacement.Lambdas,
			IfStatementSyntax or ElseClauseSyntax or ForStatementSyntax or ForEachStatementSyntax
				or ForEachVariableStatementSyntax or WhileStatementSyntax or DoStatementSyntax
				or UsingStatementSyntax or LockStatementSyntax or FixedStatementSyntax
				or TryStatementSyntax or CatchClauseSyntax or FinallyClauseSyntax
				or SwitchSectionSyntax or BlockSyntax or CheckedStatementSyntax
				or UnsafeStatementSyntax => BraceNewLinePlacement.ControlBlocks,
			_ => null
		};
	}
}
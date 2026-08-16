using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Rewriters;

using static SyntaxFactory;

/// <summary>
///   Applies every <c>csharp_space_*</c> option to an already normalised tree.
/// </summary>
/// <remarks>
///   <para>
///     <c>NormalizeWhitespace</c> has a fixed spacing policy with no knobs, so honouring these
///     options means revisiting the separator between token pairs afterwards. The pass walks the
///     flat token list and, for each adjacent pair that sits on the same line, asks whether an
///     option has an opinion about the space between them. Pairs no option covers are left exactly
///     as the normalizer produced them, which is what keeps the defaults inert.
///   </para>
///   <para>
///     Pairs separated by a line break, a comment or a directive are never touched: the separator
///     there is layout that other passes own, or trivia that carries meaning.
///   </para>
/// </remarks>
public static class SpacingRewriter
{
	/// <summary>
	///   Rewrites inter-token spacing, or returns <paramref name="node" /> untouched when every
	///   spacing option still has its default value.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node, FormattingOptions options)
	{
		if (IsDefaultSpacing(options))
		{
			return node;
		}

		var tokens = node.DescendantTokens().ToList();
		var replacements = new Dictionary<SyntaxToken, SyntaxToken>();

		for (var i = 1; i < tokens.Count; i++)
		{
			var previous = tokens[i - 1];
			var token = tokens[i];

			if (!IsSameLine(previous, token))
			{
				continue;
			}

			var wanted = WantsSpace(previous, token, options);

			if (wanted is null)
			{
				continue;
			}

			var hasSpace = previous.TrailingTrivia.Count > 0 || token.LeadingTrivia.Count > 0;

			if (hasSpace == wanted.Value)
			{
				continue;
			}

			replacements[previous] = previous.WithTrailingTrivia(wanted.Value
				? TriviaList(Space)
				: TriviaList());
			replacements[token] = token.WithLeadingTrivia(TriviaList());
		}

		return replacements.Count == 0
			? node
			: node.ReplaceTokens(replacements.Keys, (original, _) => replacements[original]);
	}

	/// <summary>
	///   Whether every spacing option still has its default value, in which case the pass is a
	///   no-op and can return immediately.
	/// </summary>
	/// <remarks>
	///   Every <c>csharp_space_*</c> field must be listed here. A field that is missing makes the
	///   pass silently do nothing for that option while the defaults stay inert and the tests stay
	///   green, so the omission would not show up in any check.
	/// </remarks>
	private static bool IsDefaultSpacing(FormattingOptions options)
	{
		var d = FormattingOptions.Default;

		return options.SpaceAfterCast == d.SpaceAfterCast
		       && options.SpaceAfterKeywordsInControlFlowStatements == d.SpaceAfterKeywordsInControlFlowStatements
		       && options.SpaceBetweenParentheses == d.SpaceBetweenParentheses
		       && options.SpaceBeforeColonInInheritanceClause == d.SpaceBeforeColonInInheritanceClause
		       && options.SpaceAfterColonInInheritanceClause == d.SpaceAfterColonInInheritanceClause
		       && options.SpaceAroundBinaryOperators == d.SpaceAroundBinaryOperators
		       && options.SpaceBetweenMethodDeclarationParameterListParentheses == d.SpaceBetweenMethodDeclarationParameterListParentheses
		       && options.SpaceBetweenMethodDeclarationEmptyParameterListParentheses == d.SpaceBetweenMethodDeclarationEmptyParameterListParentheses
		       && options.SpaceBetweenMethodDeclarationNameAndOpenParenthesis == d.SpaceBetweenMethodDeclarationNameAndOpenParenthesis
		       && options.SpaceBetweenMethodCallParameterListParentheses == d.SpaceBetweenMethodCallParameterListParentheses
		       && options.SpaceBetweenMethodCallEmptyParameterListParentheses == d.SpaceBetweenMethodCallEmptyParameterListParentheses
		       && options.SpaceBetweenMethodCallNameAndOpeningParenthesis == d.SpaceBetweenMethodCallNameAndOpeningParenthesis
		       && options.SpaceAfterComma == d.SpaceAfterComma
		       && options.SpaceBeforeComma == d.SpaceBeforeComma
		       && options.SpaceAfterDot == d.SpaceAfterDot
		       && options.SpaceBeforeDot == d.SpaceBeforeDot
		       && options.SpaceAfterSemicolonInForStatement == d.SpaceAfterSemicolonInForStatement
		       && options.SpaceBeforeSemicolonInForStatement == d.SpaceBeforeSemicolonInForStatement
		       && options.SpaceBeforeOpenSquareBrackets == d.SpaceBeforeOpenSquareBrackets
		       && options.SpaceBetweenEmptySquareBrackets == d.SpaceBetweenEmptySquareBrackets
		       && options.SpaceBetweenSquareBrackets == d.SpaceBetweenSquareBrackets;
	}

	/// <summary>
	///   Whether the two tokens are adjacent on one line with nothing but optional whitespace
	///   between them.
	/// </summary>
	private static bool IsSameLine(SyntaxToken previous, SyntaxToken token)
	{
		return IsWhitespaceOnly(previous.TrailingTrivia) && IsWhitespaceOnly(token.LeadingTrivia);
	}

	private static bool IsWhitespaceOnly(SyntaxTriviaList trivia)
	{
		foreach (var item in trivia)
		{
			if (!item.IsKind(SyntaxKind.WhitespaceTrivia))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	///   Whether the configuration wants a space between the pair, or <see langword="null" /> when
	///   no option covers it and the normalizer's choice should stand.
	/// </summary>
	private static bool? WantsSpace(SyntaxToken previous, SyntaxToken token, FormattingOptions options)
	{
		return AfterCast(previous, token, options)
		       ?? ControlFlowKeyword(previous, token, options)
		       ?? Parentheses(previous, token, options)
		       ?? InheritanceColon(previous, token, options)
		       ?? BinaryOperator(previous, token, options)
		       ?? CommaAndDot(previous, token, options)
		       ?? ForStatementSemicolon(previous, token, options)
		       ?? SquareBrackets(previous, token, options);
	}

	private static bool? AfterCast(SyntaxToken previous, SyntaxToken token, FormattingOptions options)
	{
		return previous.IsKind(SyntaxKind.CloseParenToken) && previous.Parent is CastExpressionSyntax
			? options.SpaceAfterCast
			: null;
	}

	private static bool? ControlFlowKeyword(SyntaxToken previous, SyntaxToken token, FormattingOptions options)
	{
		return token.IsKind(SyntaxKind.OpenParenToken) && IsControlFlowKeyword(previous)
			? options.SpaceAfterKeywordsInControlFlowStatements
			: null;
	}

	private static bool IsControlFlowKeyword(SyntaxToken token)
	{
		return token.RawKind switch
		{
			(int) SyntaxKind.IfKeyword or (int) SyntaxKind.ForKeyword or (int) SyntaxKind.ForEachKeyword
				or (int) SyntaxKind.WhileKeyword or (int) SyntaxKind.SwitchKeyword or (int) SyntaxKind.LockKeyword
				or (int) SyntaxKind.UsingKeyword or (int) SyntaxKind.FixedKeyword or (int) SyntaxKind.CatchKeyword => true,
			_ => false
		};
	}

	/// <summary>
	///   Handles the space just inside a parenthesis, for both
	///   <c>csharp_space_between_parentheses</c> and the six method declaration/call options.
	/// </summary>
	private static bool? Parentheses(SyntaxToken previous, SyntaxToken token, FormattingOptions options)
	{
		if (previous.IsKind(SyntaxKind.OpenParenToken))
		{
			// "()" is governed by the empty-parameter-list options, not by the inner-space ones.
			return token.IsKind(SyntaxKind.CloseParenToken)
				? EmptyParenthesesSpace(previous, options)
				: InnerParenthesesSpace(previous, previous.Parent, options);
		}

		if (token.IsKind(SyntaxKind.CloseParenToken) && !previous.IsKind(SyntaxKind.OpenParenToken))
		{
			return InnerParenthesesSpace(token, token.Parent, options);
		}

		// The space between a method's name and its opening parenthesis.
		if (token.IsKind(SyntaxKind.OpenParenToken))
		{
			return token.Parent switch
			{
				ParameterListSyntax => options.SpaceBetweenMethodDeclarationNameAndOpenParenthesis,
				ArgumentListSyntax => options.SpaceBetweenMethodCallNameAndOpeningParenthesis,
				_ => null
			};
		}

		return null;
	}

	private static bool? EmptyParenthesesSpace(SyntaxToken parenthesis, FormattingOptions options)
	{
		return parenthesis.Parent switch
		{
			ParameterListSyntax => options.SpaceBetweenMethodDeclarationEmptyParameterListParentheses,
			ArgumentListSyntax => options.SpaceBetweenMethodCallEmptyParameterListParentheses,
			_ => null
		};
	}

	private static bool? InnerParenthesesSpace(SyntaxToken parenthesis, SyntaxNode? parent, FormattingOptions options)
	{
		switch (parent)
		{
			case ParameterListSyntax:
			{
				return options.SpaceBetweenMethodDeclarationParameterListParentheses;
			}
			case ArgumentListSyntax:
			{
				return options.SpaceBetweenMethodCallParameterListParentheses;
			}
			case CastExpressionSyntax:
			{
				return options.SpaceBetweenParentheses.HasFlag(ParenthesesSpacing.TypeCasts);
			}
			case ParenthesizedExpressionSyntax:
			{
				return options.SpaceBetweenParentheses.HasFlag(ParenthesesSpacing.Expressions);
			}
			case IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or ForEachVariableStatementSyntax
				or WhileStatementSyntax or DoStatementSyntax or SwitchStatementSyntax or LockStatementSyntax
				or UsingStatementSyntax or FixedStatementSyntax or CatchClauseSyntax:
			{
				return options.SpaceBetweenParentheses.HasFlag(ParenthesesSpacing.ControlFlowStatements);
			}
			default:
			{
				_ = parenthesis;

				return null;
			}
		}
	}

	private static bool? InheritanceColon(SyntaxToken previous, SyntaxToken token, FormattingOptions options)
	{
		if (token.IsKind(SyntaxKind.ColonToken) && token.Parent is BaseListSyntax)
		{
			return options.SpaceBeforeColonInInheritanceClause;
		}

		return previous.IsKind(SyntaxKind.ColonToken) && previous.Parent is BaseListSyntax
			? options.SpaceAfterColonInInheritanceClause
			: null;
	}

	private static bool? BinaryOperator(SyntaxToken previous, SyntaxToken token, FormattingOptions options)
	{
		if (options.SpaceAroundBinaryOperators == BinaryOperatorSpacing.Ignore)
		{
			return null;
		}

		var wanted = options.SpaceAroundBinaryOperators == BinaryOperatorSpacing.BeforeAndAfter;

		if (IsBinaryOperatorToken(token) || IsBinaryOperatorToken(previous))
		{
			return wanted;
		}

		return null;
	}

	private static bool IsBinaryOperatorToken(SyntaxToken token)
	{
		return token.Parent is BinaryExpressionSyntax binary && binary.OperatorToken == token;
	}

	private static bool? CommaAndDot(SyntaxToken previous, SyntaxToken token, FormattingOptions options)
	{
		if (token.IsKind(SyntaxKind.CommaToken))
		{
			return options.SpaceBeforeComma;
		}

		if (previous.IsKind(SyntaxKind.CommaToken))
		{
			return options.SpaceAfterComma;
		}

		if (token.IsKind(SyntaxKind.DotToken))
		{
			return options.SpaceBeforeDot;
		}

		return previous.IsKind(SyntaxKind.DotToken)
			? options.SpaceAfterDot
			: null;
	}

	private static bool? ForStatementSemicolon(SyntaxToken previous, SyntaxToken token, FormattingOptions options)
	{
		if (token.IsKind(SyntaxKind.SemicolonToken) && token.Parent is ForStatementSyntax)
		{
			return options.SpaceBeforeSemicolonInForStatement;
		}

		return previous.IsKind(SyntaxKind.SemicolonToken) && previous.Parent is ForStatementSyntax
			? options.SpaceAfterSemicolonInForStatement
			: null;
	}

	private static bool? SquareBrackets(SyntaxToken previous, SyntaxToken token, FormattingOptions options)
	{
		if (token.IsKind(SyntaxKind.OpenBracketToken))
		{
			return options.SpaceBeforeOpenSquareBrackets;
		}

		if (previous.IsKind(SyntaxKind.OpenBracketToken))
		{
			return token.IsKind(SyntaxKind.CloseBracketToken)
				? options.SpaceBetweenEmptySquareBrackets
				: options.SpaceBetweenSquareBrackets;
		}

		return token.IsKind(SyntaxKind.CloseBracketToken) && !previous.IsKind(SyntaxKind.OpenBracketToken)
			? options.SpaceBetweenSquareBrackets
			: null;
	}
}
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Refactorers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Vectorize.ConstExpr.SourceGenerator.BuildIn;

namespace ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.RegexOptimizers;

/// <summary>
/// Walks a <see cref="RegexNode"/> tree produced by <see cref="RegexParser"/> and emits
/// equivalent C# statements that operate on a <c>ReadOnlySpan&lt;char&gt;</c> input.
/// Uses <see cref="FunctionOptimizerContext.OptimizeBinaryExpression"/> for character-class
/// set checks so the rewriter's binary optimizer pipeline can further simplify them.
/// </summary>
internal sealed class RegexCodeEmitter(FunctionOptimizerContext context)
{
	private int _tempVarCounter;

	/// <summary>The identifier used for the position variable inside the generated method.</summary>
	private const string PosVar = "pos";

	/// <summary>The identifier used for the input parameter.</summary>
	private const string InputVar = "input";

	// ───────────────────────────── public entry point ─────────────────────────────

	/// <summary>
	/// Emits a static <c>bool IsMatch(ReadOnlySpan&lt;char&gt; input)</c> method that
	/// is semantically equivalent to <c>Regex.IsMatch(input, pattern)</c>.
	/// </summary>
	public MethodDeclarationSyntax EmitIsMatchMethod(string pattern, RegexOptions options)
	{
		var tree = RegexParser.Parse(pattern, options, CultureInfo.InvariantCulture);

		var statements = new List<StatementSyntax>
		{
			// // int pos = 0;
			// LocalDeclarationStatement(
			// 	VariableDeclaration(IdentifierName("var"))
			// 		.WithVariables(SingletonSeparatedList(
			// 			VariableDeclarator(Identifier(PosVar))
			// 				.WithInitializer(EqualsValueClause(
			// 					LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)))))))
		};

		var pos = (int?) 0;

		EmitNode(tree.Root, statements, ref pos);

		// return true;
		statements.Add(ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression)));

		var methodName = $"IsMatch_{Math.Abs(pattern.GetHashCode()):X8}";

		return MethodDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)), Identifier(methodName))
			.WithModifiers(TokenList(Token(SyntaxKind.PrivateKeyword), Token(SyntaxKind.StaticKeyword)))
			.WithParameterList(ParameterList(SingletonSeparatedList(
				Parameter(Identifier(InputVar))
					.WithType(ParseTypeName("ReadOnlySpan<char>")))))
			.WithBody(Block(statements));
	}

	// ────────────────────────────── node dispatcher ──────────────────────────────

	private void EmitNode(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		switch (node.Kind)
		{
			// ── structural wrappers ──
			case RegexNodeKind.Capture:
			{
				EmitNode(node.Child(0), statements, ref pos);
				break;
			}

			case RegexNodeKind.Concatenate:
			{
				for (var i = 0; i < node.ChildCount(); i++)
					EmitNode(node.Child(i), statements, ref pos);
				break;
			}

			// ── anchors ──
			case RegexNodeKind.Beginning:
			{
				if (pos.HasValue)
				{
					if (pos.Value != 0)
					{
						statements.Add(ReturnFalse());
					}
				}
				else
				{
					statements.Add(IfReturnFalse(NotEqualsExpression(Pos(pos), Zero())));
				}
				break;
			}

			case RegexNodeKind.End:
			{
				statements.Add(IfReturnFalse(NotEqualsExpression(Pos(pos), InputLength())));
				break;
			}

			case RegexNodeKind.EndZ:
			{
				// Matches end of string or before \n at end
				// Fail when: pos < input.Length && (pos < input.Length - 1 || input[pos] != '\n')
				statements.Add(IfReturnFalse(
					LogicalAndExpression(
						LessThanExpression(Pos(pos), InputLength()),
						LogicalOrExpression(
							LessThanExpression(Pos(pos), SubtractExpression(InputLength(), One())),
							NotEqualsExpression(InputCharAt(Pos(pos)), CreateLiteral('\n'))))));
				break;
			}

			case RegexNodeKind.Bol:
			{
				// Beginning of line: pos == 0 || input[pos-1] == '\n'
				statements.Add(IfReturnFalse(
					LogicalAndExpression(
						NotEqualsExpression(Pos(pos), Zero()),
						NotEqualsExpression(InputCharAt(SubtractExpression(Pos(pos), One())), CreateLiteral('\n')))));
				break;
			}

			case RegexNodeKind.Eol:
			{
				// End of line: pos == input.Length || input[pos] == '\n'
				statements.Add(IfReturnFalse(
					LogicalAndExpression(
						NotEqualsExpression(Pos(pos), InputLength()),
						NotEqualsExpression(InputCharAt(Pos(pos)), CreateLiteral('\n')))));
				break;
			}

			case RegexNodeKind.Start:
			{
				// \G – only match at start; for IsMatch from 0 this is same as Beginning
				statements.Add(IfReturnFalse(NotEqualsExpression(Pos(pos), Zero())));
				break;
			}

			case RegexNodeKind.Boundary:
			{
				EmitWordBoundary(statements, negated: false, ref pos);
				break;
			}

			case RegexNodeKind.NonBoundary:
			{
				EmitWordBoundary(statements, negated: true, ref pos);
				break;
			}

			// ── single character ──
			case RegexNodeKind.One:
			{
				EmitOneChar(node, statements, ref pos);
				break;
			}

			case RegexNodeKind.Notone:
			{
				EmitNotoneChar(node, statements, ref pos);
				break;
			}

			// ── multi-character literal ──
			case RegexNodeKind.Multi:
			{
				EmitMulti(node, statements, ref pos);
				break;
			}

			// ── character class (single) ──
			case RegexNodeKind.Set:
			{
				EmitSet(node, statements, ref pos);

				if (pos.HasValue)
				{
					pos++;
				}
				else
				{
					statements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, Pos(pos))));
				}

				break;
			}

			// ── greedy / atomic loops ──
			case RegexNodeKind.Oneloop:
			case RegexNodeKind.Oneloopatomic:
			{
				EmitOneLoop(node, statements, ref pos);
				break;
			}

			case RegexNodeKind.Notoneloop:
			case RegexNodeKind.Notoneloopatomic:
			{
				EmitNotoneLoop(node, statements, ref pos);
				break;
			}

			case RegexNodeKind.Setloop:
			case RegexNodeKind.Setloopatomic:
			{
				EmitSetLoop(node, statements, ref pos);
				break;
			}

			// ── lazy loops (match minimum only — sufficient for IsMatch) ──
			case RegexNodeKind.Onelazy:
			{
				EmitOneLazy(node, statements, ref pos);
				break;
			}

			case RegexNodeKind.Notonelazy:
			{
				EmitNotoneLazy(node, statements, ref pos);
				break;
			}

			case RegexNodeKind.Setlazy:
			{
				EmitSetLazy(node, statements, ref pos);
				break;
			}

			// ── general loop / lazy loop ──
			case RegexNodeKind.Loop:
			case RegexNodeKind.Lazyloop:
			{
				EmitGeneralLoop(node, statements, ref pos);
				break;
			}

			// ── alternation ──
			case RegexNodeKind.Alternate:
			{
				EmitAlternation(node, statements, ref pos);
				break;
			}

			// ── positive lookaround ──
			case RegexNodeKind.PositiveLookaround:
			{
				EmitPositiveLookaround(node, statements, ref pos);
				break;
			}

			// ── negative lookaround ──
			case RegexNodeKind.NegativeLookaround:
			{
				EmitNegativeLookaround(node, statements, ref pos);
				break;
			}

			// ── empty / nothing ──
			case RegexNodeKind.Empty:
			{
				break; // matches empty string — no code needed
			}

			case RegexNodeKind.Nothing:
			{
				statements.Add(ReturnFalse());
				break;
			}

			// ── atomic group: treat as transparent wrapper ──
			case RegexNodeKind.Atomic:
			{
				EmitNode(node.Child(0), statements, ref pos);
				break;
			}

			// ── group (non-capturing): transparent ──
			case RegexNodeKind.Group:
			{
				EmitNode(node.Child(0), statements, ref pos);
				break;
			}

			case RegexNodeKind.UpdateBumpalong:
			{
				break; // no-op for our purposes
			}

			default:
			{
				// For unsupported node kinds, emit a comment and return false as safe fallback
				statements.Add(ReturnFalse());
				break;
			}
		}
	}

	// ────────────────────────── single character emitters ─────────────────────────

	private void EmitOneChar(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		var ch = node.Ch;
		// if (pos >= input.Length || input[pos] != 'X') return false;
		statements.Add(IfReturnFalse(
			LogicalOrExpression(
				GreaterThanOrEqualExpression(Pos(pos), InputLength()),
				NotEqualsExpression(InputCharAt(Pos(pos)), CreateLiteral(ch)))));

		// pos++;
		if (pos.HasValue)
		{
			pos++;
		}
		else
		{
			statements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, Pos(pos))));
		}
	}

	private void EmitNotoneChar(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		var ch = node.Ch;
		// if (pos >= input.Length || input[pos] == 'X') return false;
		statements.Add(IfReturnFalse(
			LogicalOrExpression(
				GreaterThanOrEqualExpression(Pos(pos), InputLength()),
				EqualsExpression(InputCharAt(Pos(pos)), CreateLiteral((ch))))));

		if (pos.HasValue)
		{
			pos++;
		}
		else
		{
			statements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, Pos(pos))));
		}
	}

	private void EmitMulti(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		var str = node.Str!;
		var len = str.Length;

		// if (!input.Slice(pos).StartsWith("Test"))
		var strLiteral = CreateLiteral(str);

		if (pos == 0)
		{
			var startsWithCall = InvocationExpression(
					MemberAccessExpression(IdentifierName(InputVar), IdentifierName("StartsWith")))
				.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(strLiteral))));

			statements.Add(IfReturnFalse(
				PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, startsWithCall)));
		}
		else
		{
			var startsWithCall = InvocationExpression(
					MemberAccessExpression(
						InvocationExpression(
								MemberAccessExpression(IdentifierName(InputVar), IdentifierName("Slice")))
							.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(Pos(pos))))),
						IdentifierName("StartsWith")))
				.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(strLiteral))));

			statements.Add(IfReturnFalse(
				PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, startsWithCall)));
		}

		// pos += len;
		if (pos.HasValue)
		{
			pos += len;
		}
		else
		{
			statements.Add(ExpressionStatement(
				AssignmentExpression(SyntaxKind.AddAssignmentExpression, Pos(pos), CreateLiteral(len))));
		}
	}

	// ──────────────────────────── character class (Set) ───────────────────────────

	/// <summary>
	/// Emits a bounds check + character-class test for a single Set node.
	/// Delegates to <see cref="FunctionOptimizerContext.OptimizeBinaryExpression"/>
	/// so the rewriter's binary optimizer pipeline can further constant-fold and simplify.
	/// </summary>
	private void EmitSet(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		// if (pos >= input.Length) return false;
		statements.Add(IfReturnFalse(GreaterThanOrEqualExpression(Pos(pos), InputLength())));

		var charExpr = InputCharAt(Pos(pos));
		var condition = EmitSetCondition(node.Str!, charExpr);

		// if (<negated ? matches : !matches>) return false;
		statements.Add(IfReturnFalse(condition));
	}

	/// <summary>
	/// Produces an <see cref="ExpressionSyntax"/> that evaluates to <c>true</c> when the
	/// character does NOT match the given character class — i.e. the condition for "return false".
	/// Uses <see cref="FunctionOptimizerContext.OptimizeBinaryExpression"/> for all binary
	/// comparisons so the rewriter's optimization pipeline can constant-fold and simplify.
	/// </summary>
	private ExpressionSyntax EmitSetCondition(string set, ExpressionSyntax charExpr)
	{
		var negated = RegexCharClass.IsNegated(set);

		switch (set)
		{
			// ── well-known classes ──
			case RegexCharClass.DigitClass or RegexCharClass.NegatedDigitClass:
			{
				var isDigit = CharMethodCall("IsDigit", charExpr);

				return set == RegexCharClass.DigitClass
					? LogicalNotExpression(isDigit)
					: isDigit;
			}
			case RegexCharClass.WordClass or RegexCharClass.NegatedWordClass:
			{
				// \w = [A-Za-z0-9_]
				var isWord = EmitIsWordChar(charExpr);

				return set == RegexCharClass.WordClass
					? LogicalNotExpression(isWord)
					: isWord;
			}
			case RegexCharClass.SpaceClass or RegexCharClass.NegatedSpaceClass:
			{
				var isSpace = CharMethodCall("IsWhiteSpace", charExpr);

				return set == RegexCharClass.SpaceClass
					? LogicalNotExpression(isSpace)
					: isSpace;
			}
			case RegexCharClass.NotDigitClass:
			{
				return CharMethodCall("IsDigit", charExpr);
			}
			case RegexCharClass.NotSpaceClass:
			{
				return CharMethodCall("IsWhiteSpace", charExpr);
			}
			case RegexCharClass.NotWordClass:
			{
				return EmitIsWordChar(charExpr);
			}
			case RegexCharClass.LetterClass or RegexCharClass.NotLetterClass:
			{
				var isLetter = CharMethodCall("IsLetter", charExpr);

				return set == RegexCharClass.LetterClass
					? LogicalNotExpression(isLetter)
					: isLetter;
			}
			case RegexCharClass.LetterOrDigitClass or RegexCharClass.NotLetterOrDigitClass:
			{
				var isLetterOrDigit = CharMethodCall("IsLetterOrDigit", charExpr);

				return set == RegexCharClass.LetterOrDigitClass
					? LogicalNotExpression(isLetterOrDigit)
					: isLetterOrDigit;
			}
			case RegexCharClass.LowerClass or RegexCharClass.NotLowerClass:
			{
				var isLower = CharMethodCall("IsLower", charExpr);

				return set == RegexCharClass.LowerClass
					? LogicalNotExpression(isLower)
					: isLower;
			}
			case RegexCharClass.UpperClass or RegexCharClass.NotUpperClass:
			{
				var isUpper = CharMethodCall("IsUpper", charExpr);

				return set == RegexCharClass.UpperClass
					? LogicalNotExpression(isUpper)
					: isUpper;
			}
			case RegexCharClass.ControlClass or RegexCharClass.NotControlClass:
			{
				var isControl = CharMethodCall("IsControl", charExpr);

				return set == RegexCharClass.ControlClass
					? LogicalNotExpression(isControl)
					: isControl;
			}
			case RegexCharClass.AnyClass:
			{
				// Any char matches — never fails (condition for "return false" is... false)
				return LiteralExpression(SyntaxKind.FalseLiteralExpression);
			}
		}

		// ── singleton set ──
		if (RegexCharClass.IsSingleton(set))
		{
			var ch = RegexCharClass.SingletonChar(set);
			return OptimizedNotEqual(charExpr, CreateLiteral(ch));
		}

		if (RegexCharClass.IsSingletonInverse(set))
		{
			var ch = RegexCharClass.SingletonChar(set);
			return OptimizedEqual(charExpr, CreateLiteral(ch));
		}

		// ── single range [a-z] ──
		if (RegexCharClass.TryGetSingleRange(set, out var lo, out var hi))
		{
			var inRange = OptimizedLogicalAnd(
				OptimizedGreaterThanOrEqual(charExpr, CreateLiteral(lo)),
				OptimizedLessThanOrEqual(charExpr, CreateLiteral(hi)));

			if (!negated)
			{
				if (InvertLogicalRefactoring.TryInvertLogical(inRange as BinaryExpressionSyntax, out var newInRange))
				{
					inRange = newInRange;
				}
				else
				{
					inRange = LogicalNotExpression(ParenthesizedExpression(inRange));
				}
			}

			return inRange;
		}

		// ── double range [A-Za-z] ──
		if (RegexCharClass.TryGetDoubleRange(set, out var r0, out var r1))
		{
			var range0 = ParenthesizedExpression(OptimizedLogicalAnd(
				OptimizedGreaterThanOrEqual(charExpr, CreateLiteral(r0.LowInclusive)),
				OptimizedLessThanOrEqual(charExpr, CreateLiteral(r0.HighInclusive))));

			var range1 = ParenthesizedExpression(OptimizedLogicalAnd(
				OptimizedGreaterThanOrEqual(charExpr, CreateLiteral(r1.LowInclusive)),
				OptimizedLessThanOrEqual(charExpr, CreateLiteral(r1.HighInclusive))));

			var inEither = OptimizedLogicalOr(range0, range1);

			if (!negated)
			{
				if (InvertLogicalRefactoring.TryInvertLogical(inEither as BinaryExpressionSyntax, out var newInEither))
				{
					inEither = newInEither;
				}
				else
				{
					inEither = LogicalNotExpression(ParenthesizedExpression(inEither));
				}
			}

			return inEither;
		}

		// ── small enumerable set — expand to OR-chain ──
		Span<char> chars = stackalloc char[5];
		var count = RegexCharClass.GetSetChars(set, chars);

		if (count is > 0 and <= 5)
		{
			ExpressionSyntax? matchExpr = null;

			for (var i = 0; i < count; i++)
			{
				var eq = OptimizedEqual(charExpr, CreateLiteral(chars[i]));
				matchExpr = matchExpr is null ? eq : OptimizedLogicalOr(matchExpr, eq);
			}

			if (negated)
			{
				// Negated: chars are the ones that DON'T match → if char is one of them → fail
				return matchExpr!;
			}

			// Not negated: chars are the ones that DO match → if char is NOT one of them → fail
			return LogicalNotExpression(ParenthesizedExpression(matchExpr!));
		}

		// ── fallback: enumerate all ranges from the set ──
		return EmitSetRangesFallback(set, charExpr, negated);
	}

	/// <summary>
	/// Fallback for character classes that are not a well-known class, singleton, or simple range.
	/// Enumerates the raw range pairs from the set string, then also handles any Unicode categories
	/// present in the class (e.g. <c>\s</c>, <c>\d</c> combined with literal ranges).
	/// </summary>
	private ExpressionSyntax EmitSetRangesFallback(string set, ExpressionSyntax charExpr, bool negated)
	{
		var setLength = (int) set[RegexCharClass.SetLengthIndex];
		ExpressionSyntax? matchExpr = null;

		for (var i = RegexCharClass.SetStartIndex; i < RegexCharClass.SetStartIndex + setLength; i += 2)
		{
			var rangeLo = set[i];
			var rangeHi = i + 1 < RegexCharClass.SetStartIndex + setLength
				? (char) (set[i + 1] - 1)
				: '\uFFFF';

			ExpressionSyntax rangeCheck;

			if (rangeLo == rangeHi)
			{
				rangeCheck = OptimizedEqual(charExpr, CreateLiteral(rangeLo));
			}
			else
			{
				rangeCheck = ParenthesizedExpression(OptimizedLogicalAnd(
					OptimizedGreaterThanOrEqual(charExpr, CreateLiteral(rangeLo)),
					OptimizedLessThanOrEqual(charExpr, CreateLiteral(rangeHi))));
			}

			matchExpr = matchExpr is null ? rangeCheck : OptimizedLogicalOr(matchExpr, rangeCheck);
		}

		// Also handle categories (e.g. \s, \d) that are part of the class
		var categoryExpr = EmitCategoryMatchExpression(set, charExpr);

		if (categoryExpr is not null)
		{
			matchExpr = matchExpr is null ? categoryExpr : OptimizedLogicalOr(matchExpr, categoryExpr);
		}

		if (matchExpr is null)
		{
			// Empty set: nothing matches
			return CreateLiteral(!negated);
		}

		if (!negated)
		{
			if (InvertLogicalRefactoring.TryInvertLogical(matchExpr as BinaryExpressionSyntax, out var newMatchExpr))
			{
				matchExpr = newMatchExpr;
			}
			else
			{
				matchExpr = LogicalNotExpression(ParenthesizedExpression(matchExpr));
			}
		}

		return matchExpr;
	}

	/// <summary>
	/// Emits an expression that evaluates to <c>true</c> when the character matches the
	/// category portion of a character class string. Returns <c>null</c> if the class has
	/// no categories.
	/// </summary>
	/// <remarks>
	/// Handles the <c>SpaceConst</c> pseudo-category (<c>\s</c>), individual Unicode categories,
	/// and category groups (e.g. <c>\w</c> which is a group of letter + digit + connector categories).
	/// </remarks>
	private ExpressionSyntax? EmitCategoryMatchExpression(string set, ExpressionSyntax charExpr)
	{
		var setLength = (int) set[RegexCharClass.SetLengthIndex];
		var categoryLength = (int) set[RegexCharClass.CategoryLengthIndex];

		if (categoryLength == 0)
		{
			return null;
		}

		var categoryStart = RegexCharClass.SetStartIndex + setLength;
		var categoryEnd = categoryStart + categoryLength;
		ExpressionSyntax? result = null;

		for (var i = categoryStart; i < categoryEnd; i++)
		{
			var curcat = (short) set[i];

			if (curcat == 0)
			{
				// Category group — read until next 0 delimiter
				i++;

				if (i >= categoryEnd)
				{
					break;
				}

				var first = (short) set[i];

				if (first > 0)
				{
					// Positive group: char must be in ANY of the categories → OR
					ExpressionSyntax? groupExpr = null;

					while (i < categoryEnd && (short) set[i] != 0)
					{
						var check = EmitSingleCategoryMatch(charExpr, (short) set[i]);
						groupExpr = groupExpr is null ? check : OptimizedLogicalOr(groupExpr, check);
						i++;
					}

					if (groupExpr is not null)
					{
						result = result is null ? groupExpr : OptimizedLogicalOr(result, ParenthesizedExpression(groupExpr));
					}
				}
				else
				{
					// Negative group: char must be in NONE of the categories → AND of !=
					ExpressionSyntax? groupExpr = null;

					while (i < categoryEnd && (short) set[i] != 0)
					{
						var cat = -1 - (short) set[i];
						var check = EmitUnicodeCategoryNotEquals(charExpr, cat);
						groupExpr = groupExpr is null ? check : OptimizedLogicalAnd(groupExpr, check);
						i++;
					}

					if (groupExpr is not null)
					{
						result = result is null ? groupExpr : OptimizedLogicalOr(result, ParenthesizedExpression(groupExpr));
					}
				}
			}
			else if (curcat == RegexCharClass.SpaceConst)
			{
				var check = CharMethodCall("IsWhiteSpace", charExpr);
				result = result is null ? check : OptimizedLogicalOr(result, check);
			}
			else if (curcat == -RegexCharClass.SpaceConst)
			{
				var check = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, CharMethodCall("IsWhiteSpace", charExpr));
				result = result is null ? check : OptimizedLogicalOr(result, check);
			}
			else if (curcat > 0)
			{
				var check = EmitSingleCategoryMatch(charExpr, curcat);
				result = result is null ? check : OptimizedLogicalOr(result, check);
			}
			else // curcat < 0
			{
				var cat = -1 - curcat;
				var check = EmitUnicodeCategoryNotEquals(charExpr, cat);
				result = result is null ? check : OptimizedLogicalOr(result, check);
			}
		}

		return result;
	}

	/// <summary>
	/// Emits an expression for a single positive Unicode category match.
	/// Uses <c>char.IsDigit</c>, <c>char.IsLetter</c>, etc. for common categories,
	/// and falls back to <c>(int)char.GetUnicodeCategory(ch) == N</c> for others.
	/// </summary>
	private ExpressionSyntax EmitSingleCategoryMatch(ExpressionSyntax charExpr, short curcat)
	{
		var unicodeCategory = curcat - 1;
		return unicodeCategory switch
		{
			0 => CharMethodCall("IsUpper", charExpr), // UppercaseLetter
			1 => CharMethodCall("IsLower", charExpr), // LowercaseLetter
			8 => CharMethodCall("IsDigit", charExpr), // DecimalDigitNumber
			14 => CharMethodCall("IsControl", charExpr), // OtherControl → Control
			_ => EmitUnicodeCategoryEquals(charExpr, unicodeCategory)
		};
	}

	private ExpressionSyntax EmitUnicodeCategoryEquals(ExpressionSyntax charExpr, int categoryValue)
	{
		return OptimizedEqual(
			CastExpression(PredefinedType(Token(SyntaxKind.IntKeyword)),
				InvocationExpression(
						MemberAccessExpression(CreateTypeSyntax<char>(), IdentifierName("GetUnicodeCategory")))
					.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(charExpr))))),
			CreateLiteral(categoryValue));
	}

	private ExpressionSyntax EmitUnicodeCategoryNotEquals(ExpressionSyntax charExpr, int categoryValue)
	{
		return OptimizedNotEqual(
			CastExpression(PredefinedType(Token(SyntaxKind.IntKeyword)),
				InvocationExpression(
						MemberAccessExpression(CreateTypeSyntax<char>(), IdentifierName("GetUnicodeCategory")))
					.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(charExpr))))),
			CreateLiteral(categoryValue));
	}

	// ──────────────────────────── greedy loop emitters ────────────────────────────

	private void EmitOneLoop(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		var countVar = NextVar("count");
		var max = node.N == int.MaxValue ? InputLength() : CreateLiteral(node.N);

		// int countN = 0;
		statements.Add(DeclareIntVar(countVar, 0));

		// while (countN < max && pos + countN < input.Length && input[pos + countN] == 'X') countN++;
		statements.Add(WhileStatement(
			LogicalAndExpression(
				LogicalAndExpression(
					LessThanExpression(IdentifierName(countVar), max),
					LessThanExpression(AddWithPos(pos, IdentifierName(countVar)), InputLength())),
				EqualsExpression(InputCharAt(AddWithPos(pos, IdentifierName(countVar))), CreateLiteral(node.Ch))),
			Block(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(countVar))))));

		if (node.M > 0)
		{
			statements.Add(IfReturnFalse(LessThanExpression(IdentifierName(countVar), CreateLiteral(node.M))));
		}

		// pos += countN;
		if (pos.HasValue)
		{
			statements.Add(CreatePosDeclaration(AddWithPos(pos, IdentifierName(countVar))));
			pos = null;
		}
		else
		{
			statements.Add(ExpressionStatement(
				AssignmentExpression(SyntaxKind.AddAssignmentExpression, Pos(pos), IdentifierName(countVar))));
		}
	}

	private void EmitNotoneLoop(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		var countVar = NextVar("count");
		var max = node.N == int.MaxValue ? InputLength() : CreateLiteral(node.N);

		statements.Add(DeclareIntVar(countVar, 0));
		statements.Add(WhileStatement(
			LogicalAndExpression(
				LogicalAndExpression(
					LessThanExpression(IdentifierName(countVar), max),
					LessThanExpression(AddWithPos(pos, IdentifierName(countVar)), InputLength())),
				NotEqualsExpression(InputCharAt(AddWithPos(pos, IdentifierName(countVar))), CreateLiteral(node.Ch))),
			Block(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(countVar))))));

		if (node.M > 0)
		{
			statements.Add(IfReturnFalse(LessThanExpression(IdentifierName(countVar), CreateLiteral(node.M))));
		}

		// pos += count;
		if (pos.HasValue)
		{
			statements.Add(CreatePosDeclaration(AddWithPos(pos, IdentifierName(countVar))));
			pos = null;
		}
		else
		{
			statements.Add(ExpressionStatement(
				AssignmentExpression(SyntaxKind.AddAssignmentExpression, Pos(pos), IdentifierName(countVar))));
		}
	}

	private void EmitSetLoop(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		var countVar = NextVar("count");
		var max = node.N == int.MaxValue ? InputLength() : CreateLiteral(node.N);
		var charAtExpr = InputCharAt(AddWithPos(pos, IdentifierName(countVar)));

		// The condition for MATCHING is the negation of the "return false" condition
		var matchesCondition = EmitSetCondition(node.Str!, charAtExpr);
		// EmitSetCondition returns "does NOT match" → negate to get "does match"
		var loopCondition = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(matchesCondition));

		statements.Add(DeclareIntVar(countVar, 0));
		statements.Add(WhileStatement(
			LogicalAndExpression(
				OptimizedLogicalAnd(
					LessThanExpression(IdentifierName(countVar), max),
					LessThanExpression(AddWithPos(pos, IdentifierName(countVar)), InputLength())),
				loopCondition),
			Block(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(countVar))))));

		if (node.M > 0)
		{
			statements.Add(IfReturnFalse(LessThanExpression(IdentifierName(countVar), CreateLiteral(node.M))));
		}

		// pos += count;
		if (pos.HasValue)
		{
			statements.Add(CreatePosDeclaration(AddWithPos(pos, IdentifierName(countVar))));
			pos = null;
		}
		else
		{
			statements.Add(ExpressionStatement(
				AssignmentExpression(SyntaxKind.AddAssignmentExpression, Pos(pos), IdentifierName(countVar))));
		}
	}

	// ─────────────────────────── lazy loop emitters ───────────────────────────────

	private void EmitOneLazy(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		// For IsMatch, lazy loops match the minimum count
		EmitFixedRepeat(statements, node.M, charExpr => EqualsExpression(charExpr, CreateLiteral(node.Ch)), ref pos);
	}

	private void EmitNotoneLazy(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		EmitFixedRepeat(statements, node.M, charExpr => NotEqualsExpression(charExpr, CreateLiteral(node.Ch)), ref pos);
	}

	private void EmitSetLazy(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		// For lazy set loops, match the minimum count using the set condition
		for (var i = 0; i < node.M; i++)
		{
			statements.Add(IfReturnFalse(GreaterThanOrEqualExpression(Pos(pos), InputLength())));

			var charExpr = InputCharAt(Pos(pos));
			var doesNotMatch = EmitSetCondition(node.Str!, charExpr);
			statements.Add(IfReturnFalse(doesNotMatch));

			if (pos.HasValue)
			{
				pos++;
			}
			else
			{
				statements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, Pos(pos))));
			}
		}
	}

	// ─────────────────────────── general loop emitters ────────────────────────────

	private void EmitGeneralLoop(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		var iterVar = NextVar("iter");
		var child = node.Child(0);

		statements.Add(DeclareIntVar(iterVar, 0));

		// Build the loop body: try to match child, if success increment iter
		var bodyStatements = new List<StatementSyntax>();
		var savedPosVar = NextVar("savedPos");
		bodyStatements.Add(DeclareIntVar(savedPosVar, Pos(pos)));

		var childStatements = new List<StatementSyntax>();
		EmitNode(child, childStatements, ref pos);

		// We wrap child matching in a block. If any "return false" fires, we break.
		// Approach: use a local function to test child, break on failure.
		var testMethodName = NextVar("TryMatchChild");

		var testMethod = LocalFunctionStatement(
				PredefinedType(Token(SyntaxKind.BoolKeyword)),
				Identifier(testMethodName))
			.WithBody(Block(childStatements.Append(ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression)))));

		bodyStatements.Add(testMethod);
		bodyStatements.Add(IfStatement(
			PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
				InvocationExpression(IdentifierName(testMethodName))),
			Block(
				ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, Pos(pos), IdentifierName(savedPosVar))),
				BreakStatement())));

		bodyStatements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(iterVar))));

		var maxExpr = node.N == int.MaxValue
			? MemberAccessExpression(
				PredefinedType(Token(SyntaxKind.IntKeyword)), IdentifierName("MaxValue"))
			: CreateLiteral(node.N);

		statements.Add(WhileStatement(
			LessThanExpression(IdentifierName(iterVar), maxExpr),
			Block(bodyStatements)));

		if (node.M > 0)
		{
			statements.Add(IfReturnFalse(LessThanExpression(IdentifierName(iterVar), CreateLiteral(node.M))));
		}
	}

	// ────────────────────────── alternation emitter ───────────────────────────────

	private void EmitAlternation(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		var childCount = node.ChildCount();

		if (childCount == 0)
		{
			statements.Add(ReturnFalse());
			return;
		}

		// Save position and try each branch; on success, jump past the alternation.
		// We generate a local function per branch for cleanness.
		var savedPosVar = NextVar("altPos");
		statements.Add(DeclareIntVar(savedPosVar, Pos(pos)));

		var matchedVar = NextVar("altMatched");
		statements.Add(LocalDeclarationStatement(
			VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier(matchedVar))
						.WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression)))))));

		for (var i = 0; i < childCount; i++)
		{
			var branchStatements = new List<StatementSyntax>();
			EmitNode(node.Child(i), branchStatements, ref pos);
			// If we reach here, the branch succeeded
			branchStatements.Add(ExpressionStatement(
				AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
					IdentifierName(matchedVar),
					LiteralExpression(SyntaxKind.TrueLiteralExpression))));

			var branchMethodName = NextVar("TryBranch");
			var branchFunc = LocalFunctionStatement(
					PredefinedType(Token(SyntaxKind.BoolKeyword)),
					Identifier(branchMethodName))
				.WithBody(Block(branchStatements.Append(ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression)))));

			statements.Add(branchFunc);

			// pos = altPosN; if (TryBranchN()) goto done;

			// pos += count;
			if (pos.HasValue)
			{
				statements.Add(CreatePosDeclaration(IdentifierName(savedPosVar)));
				pos = null;
			}
			else
			{
				statements.Add(ExpressionStatement(
					AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, Pos(pos), IdentifierName(savedPosVar))));
			}

			statements.Add(IfStatement(
				InvocationExpression(IdentifierName(branchMethodName)),
				Block(
					ExpressionStatement(
						AssignmentExpression(SyntaxKind.SimpleAssignmentExpression,
							IdentifierName(matchedVar),
							LiteralExpression(SyntaxKind.TrueLiteralExpression))))));

			// If matched, skip remaining branches
			if (i < childCount - 1)
			{
				// Wrap remaining branches in if (!matched) { ... }
				// For simplicity we just check at the beginning of each subsequent iteration
			}
		}

		// if (!altMatched) return false;
		statements.Add(IfReturnFalse(
			PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, IdentifierName(matchedVar))));
	}

	// ────────────────────────── lookaround emitters ──────────────────────────────

	private void EmitPositiveLookaround(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		var savedPosVar = NextVar("laPos");
		statements.Add(DeclareIntVar(savedPosVar, Pos(pos)));

		var childStatements = new List<StatementSyntax>();
		EmitNode(node.Child(0), childStatements, ref pos);

		var testName = NextVar("Lookahead");
		var testFunc = LocalFunctionStatement(
				PredefinedType(Token(SyntaxKind.BoolKeyword)),
				Identifier(testName))
			.WithBody(Block(childStatements.Append(ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression)))));

		statements.Add(testFunc);

		// if (!Lookahead()) return false;
		statements.Add(IfReturnFalse(
			PrefixUnaryExpression(SyntaxKind.LogicalNotExpression,
				InvocationExpression(IdentifierName(testName)))));

		// Restore position (lookahead doesn't consume)
		if (pos.HasValue)
		{
			statements.Add(CreatePosDeclaration(IdentifierName(savedPosVar)));
			pos = null;
		}
		else
		{
			statements.Add(ExpressionStatement(
				AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, Pos(pos), IdentifierName(savedPosVar))));
		}
	}

	private void EmitNegativeLookaround(RegexNode node, List<StatementSyntax> statements, ref int? pos)
	{
		var savedPosVar = NextVar("nlPos");
		statements.Add(DeclareIntVar(savedPosVar, Pos(pos)));

		var childStatements = new List<StatementSyntax>();
		EmitNode(node.Child(0), childStatements, ref pos);

		var testName = NextVar("NegLookahead");
		var testFunc = LocalFunctionStatement(
				PredefinedType(Token(SyntaxKind.BoolKeyword)),
				Identifier(testName))
			.WithBody(Block(childStatements.Append(ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression)))));

		statements.Add(testFunc);

		// if (NegLookahead()) return false; — if it DID match, the negative lookahead fails
		statements.Add(IfReturnFalse(InvocationExpression(IdentifierName(testName))));

		// Restore position
		if (pos.HasValue)
		{
			statements.Add(CreatePosDeclaration(IdentifierName(savedPosVar)));
			pos = null;
		}
		else
		{
			statements.Add(ExpressionStatement(
				AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, Pos(pos), IdentifierName(savedPosVar))));
		}
	}

	// ────────────────────────── word boundary emitter ─────────────────────────────

	private void EmitWordBoundary(List<StatementSyntax> statements, bool negated, ref int? pos)
	{
		// Word boundary: the character on one side is a word char and the other is not (or is boundary of string).
		// bool leftIsWord = pos > 0 && IsWordChar(input[pos-1]);
		// bool rightIsWord = pos < input.Length && IsWordChar(input[pos]);
		// if (leftIsWord == rightIsWord) return false;  // for \b
		// if (leftIsWord != rightIsWord) return false;  // for \B

		var leftVar = NextVar("leftWord");
		var rightVar = NextVar("rightWord");

		statements.Add(LocalDeclarationStatement(
			VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier(leftVar))
						.WithInitializer(EqualsValueClause(
							LogicalAndExpression(
								GreaterThanExpression(Pos(pos), Zero()),
								EmitIsWordChar(InputCharAt(SubtractExpression(Pos(pos), One()))))))))));

		statements.Add(LocalDeclarationStatement(
			VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier(rightVar))
						.WithInitializer(EqualsValueClause(
							LogicalAndExpression(
								LessThanExpression(Pos(pos), InputLength()),
								EmitIsWordChar(InputCharAt(Pos(pos))))))))));

		var condition = negated
			? NotEqualsExpression(IdentifierName(leftVar), IdentifierName(rightVar)) // \B: fail when they differ
			: EqualsExpression(IdentifierName(leftVar), IdentifierName(rightVar)); // \b: fail when they're same

		statements.Add(IfReturnFalse(condition));
	}

	/// <summary>
	/// Emits <c>char.IsLetterOrDigit(ch) || ch == '_'</c> using OptimizeBinaryExpression.
	/// </summary>
	private ExpressionSyntax EmitIsWordChar(ExpressionSyntax charExpr)
	{
		return OptimizedLogicalOr(
			CharMethodCall("IsLetterOrDigit", charExpr),
			OptimizedEqual(charExpr, CreateLiteral('_')));
	}

	// ────────────────────────── helper: fixed repeat ─────────────────────────────

	/// <summary>
	/// Emits code that matches exactly <paramref name="count"/> characters that satisfy
	/// the given <paramref name="charCondition"/> (which returns true when the character MATCHES).
	/// </summary>
	private void EmitFixedRepeat(List<StatementSyntax> statements, int count, Func<ExpressionSyntax, ExpressionSyntax> charCondition, ref int? pos)
	{
		if (count == 0)
		{
			return;
		}

		// if (pos + count > input.Length) return false;
		statements.Add(IfReturnFalse(GreaterThanExpression(AddWithPos(pos, CreateLiteral(count)), InputLength())));

		for (var i = 0; i < count; i++)
		{
			// if (!condition(input[pos])) return false;
			var charExpr = InputCharAt(Pos(pos));
			var matches = charCondition(charExpr);

			matches = InvertLogicalRefactoring.TryInvertLogical(matches as BinaryExpressionSyntax, out var newMatches)
				? newMatches
				: LogicalNotExpression(ParenthesizedExpression(matches));

			statements.Add(IfReturnFalse(matches));

			if (pos.HasValue)
			{
				pos++;
			}
			else
			{
				statements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, Pos(pos))));
			}
		}
	}

	// ══════════════════════════ Syntax Factory Helpers ════════════════════════════

	private string NextVar(string prefix) => $"{prefix}{_tempVarCounter++}";

	private static ExpressionSyntax Pos(int? value)
	{
		if (value.HasValue)
		{
			return CreateLiteral(value.Value);
		}

		return IdentifierName(PosVar);
	}

	private static LocalDeclarationStatementSyntax CreatePosDeclaration(ExpressionSyntax initialValue)
	{
		return LocalDeclarationStatement(
			VariableDeclaration(IdentifierName("var"))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier(PosVar))
						.WithInitializer(EqualsValueClause(
							initialValue)))));
	}

	private static ExpressionSyntax AddWithPos(int? pos, ExpressionSyntax right)
	{
		if (pos == 0)
		{
			return right;
		}

		if (pos != null)
		{
			return BinaryExpression(SyntaxKind.AddExpression, right, CreateLiteral(pos.Value));
		}
		
		return BinaryExpression(SyntaxKind.AddExpression, IdentifierName(PosVar), right);
	}

	private static ExpressionSyntax InputLength() =>
		MemberAccessExpression(IdentifierName(InputVar), IdentifierName("Length"));

	private static ExpressionSyntax InputCharAt(ExpressionSyntax index) =>
		ElementAccessExpression(IdentifierName(InputVar))
			.WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(index))));

	private static ExpressionSyntax Zero() => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0));
	private static ExpressionSyntax One() => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1));

	private static InvocationExpressionSyntax CharMethodCall(string method, ExpressionSyntax arg) =>
		InvocationExpression(
				MemberAccessExpression(CreateTypeSyntax<char>(), IdentifierName(method)))
			.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(arg))));

	// ── binary helpers that delegate to OptimizeBinaryExpression ──

	private ExpressionSyntax OptimizedEqual(ExpressionSyntax left, ExpressionSyntax right) =>
		context.OptimizeBinaryExpression(
			BinaryExpression(SyntaxKind.EqualsExpression, left, right),
			context.Model.Compilation.GetSpecialType(SpecialType.System_Char),
			context.Model.Compilation.GetSpecialType(SpecialType.System_Char),
			context.Model.Compilation.GetSpecialType(SpecialType.System_Boolean));

	private ExpressionSyntax OptimizedNotEqual(ExpressionSyntax left, ExpressionSyntax right) =>
		context.OptimizeBinaryExpression(
			BinaryExpression(SyntaxKind.NotEqualsExpression, left, right),
			context.Model.Compilation.GetSpecialType(SpecialType.System_Char),
			context.Model.Compilation.GetSpecialType(SpecialType.System_Char),
			context.Model.Compilation.GetSpecialType(SpecialType.System_Boolean));

	private ExpressionSyntax OptimizedGreaterThanOrEqual(ExpressionSyntax left, ExpressionSyntax right) =>
		context.OptimizeBinaryExpression(
			BinaryExpression(SyntaxKind.GreaterThanOrEqualExpression, left, right),
			context.Model.Compilation.GetSpecialType(SpecialType.System_Char),
			context.Model.Compilation.GetSpecialType(SpecialType.System_Char),
			context.Model.Compilation.GetSpecialType(SpecialType.System_Boolean));

	private ExpressionSyntax OptimizedLessThanOrEqual(ExpressionSyntax left, ExpressionSyntax right) =>
		context.OptimizeBinaryExpression(
			BinaryExpression(SyntaxKind.LessThanOrEqualExpression, left, right),
			context.Model.Compilation.GetSpecialType(SpecialType.System_Char),
			context.Model.Compilation.GetSpecialType(SpecialType.System_Char),
			context.Model.Compilation.GetSpecialType(SpecialType.System_Boolean));

	private ExpressionSyntax OptimizedLogicalAnd(ExpressionSyntax left, ExpressionSyntax right) =>
		context.OptimizeBinaryExpression(
			BinaryExpression(SyntaxKind.LogicalAndExpression, left, right),
			context.Model.Compilation.CreateBoolean(),
			context.Model.Compilation.CreateBoolean(),
			context.Model.Compilation.CreateBoolean());

	private ExpressionSyntax OptimizedLogicalOr(ExpressionSyntax left, ExpressionSyntax right) =>
		context.OptimizeBinaryExpression(
			BinaryExpression(SyntaxKind.LogicalOrExpression, left, right),
			context.Model.Compilation.CreateBoolean(),
			context.Model.Compilation.CreateBoolean(),
			context.Model.Compilation.CreateBoolean());


	// ── statement helpers ──

	private static StatementSyntax IfReturnFalse(ExpressionSyntax condition) =>
		IfStatement(condition, Block(ReturnFalse()));

	private static ReturnStatementSyntax ReturnFalse() =>
		ReturnStatement(LiteralExpression(SyntaxKind.FalseLiteralExpression));

	private static LocalDeclarationStatementSyntax DeclareIntVar(string name, int value) =>
		LocalDeclarationStatement(
			VariableDeclaration(IdentifierName("var"))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier(name))
						.WithInitializer(EqualsValueClause(CreateLiteral(value))))));

	private static LocalDeclarationStatementSyntax DeclareIntVar(string name, ExpressionSyntax value) =>
		LocalDeclarationStatement(
			VariableDeclaration(IdentifierName("var"))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier(name))
						.WithInitializer(EqualsValueClause(value)))));
}
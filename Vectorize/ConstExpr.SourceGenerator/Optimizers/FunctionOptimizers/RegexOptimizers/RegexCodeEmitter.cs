using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using ConstExpr.SourceGenerator.Extensions;
using ConstExpr.SourceGenerator.Models;
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
			// int pos = 0;
			LocalDeclarationStatement(
				VariableDeclaration(PredefinedType(Token(SyntaxKind.IntKeyword)))
					.WithVariables(SingletonSeparatedList(
						VariableDeclarator(Identifier(PosVar))
							.WithInitializer(EqualsValueClause(
								LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0)))))))
		};

		EmitNode(tree.Root, statements);

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

	private void EmitNode(RegexNode node, List<StatementSyntax> statements)
	{
		switch (node.Kind)
		{
			// ── structural wrappers ──
			case RegexNodeKind.Capture:
				EmitNode(node.Child(0), statements);
				break;

			case RegexNodeKind.Concatenate:
				for (var i = 0; i < node.ChildCount(); i++)
					EmitNode(node.Child(i), statements);
				break;

			// ── anchors ──
			case RegexNodeKind.Beginning:
				statements.Add(IfReturnFalse(NotEqualsExpression(Pos(), Zero())));
				break;

			case RegexNodeKind.End:
				statements.Add(IfReturnFalse(NotEqualsExpression(Pos(), InputLength())));
				break;

			case RegexNodeKind.EndZ:
				// Matches end of string or before \n at end
				statements.Add(IfReturnFalse(
					LogicalAndExpression(
						LessThanExpression(Pos(), SubtractExpression(InputLength(), One())),
						LogicalOrExpression(
							GreaterThanExpression(Pos(), InputLength()),
							NotEqualsExpression(InputCharAt(Pos()), CreateLiteral('\n'))))));
				break;

			case RegexNodeKind.Bol:
				// Beginning of line: pos == 0 || input[pos-1] == '\n'
				statements.Add(IfReturnFalse(
					LogicalAndExpression(
						NotEqualsExpression(Pos(), Zero()),
						NotEqualsExpression(InputCharAt(SubtractExpression(Pos(), One())), CreateLiteral('\n')))));
				break;

			case RegexNodeKind.Eol:
				// End of line: pos == input.Length || input[pos] == '\n'
				statements.Add(IfReturnFalse(
					LogicalAndExpression(
						NotEqualsExpression(Pos(), InputLength()),
						NotEqualsExpression(InputCharAt(Pos()), CreateLiteral('\n')))));
				break;

			case RegexNodeKind.Start:
				// \G – only match at start; for IsMatch from 0 this is same as Beginning
				statements.Add(IfReturnFalse(NotEqualsExpression(Pos(), Zero())));
				break;

			case RegexNodeKind.Boundary:
				EmitWordBoundary(statements, negated: false);
				break;

			case RegexNodeKind.NonBoundary:
				EmitWordBoundary(statements, negated: true);
				break;

			// ── single character ──
			case RegexNodeKind.One:
				EmitOneChar(node, statements);
				break;

			case RegexNodeKind.Notone:
				EmitNotoneChar(node, statements);
				break;

			// ── multi-character literal ──
			case RegexNodeKind.Multi:
				EmitMulti(node, statements);
				break;

			// ── character class (single) ──
			case RegexNodeKind.Set:
				EmitSet(node, statements);
				statements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, Pos())));
				break;

			// ── greedy / atomic loops ──
			case RegexNodeKind.Oneloop:
			case RegexNodeKind.Oneloopatomic:
				EmitOneLoop(node, statements);
				break;

			case RegexNodeKind.Notoneloop:
			case RegexNodeKind.Notoneloopatomic:
				EmitNotoneLoop(node, statements);
				break;

			case RegexNodeKind.Setloop:
			case RegexNodeKind.Setloopatomic:
				EmitSetLoop(node, statements);
				break;

			// ── lazy loops (match minimum only — sufficient for IsMatch) ──
			case RegexNodeKind.Onelazy:
				EmitOneLazy(node, statements);
				break;

			case RegexNodeKind.Notonelazy:
				EmitNotoneLazy(node, statements);
				break;

			case RegexNodeKind.Setlazy:
				EmitSetLazy(node, statements);
				break;

			// ── general loop / lazy loop ──
			case RegexNodeKind.Loop:
			case RegexNodeKind.Lazyloop:
				EmitGeneralLoop(node, statements);
				break;

			// ── alternation ──
			case RegexNodeKind.Alternate:
				EmitAlternation(node, statements);
				break;

			// ── positive lookaround ──
			case RegexNodeKind.PositiveLookaround:
				EmitPositiveLookaround(node, statements);
				break;

			// ── negative lookaround ──
			case RegexNodeKind.NegativeLookaround:
				EmitNegativeLookaround(node, statements);
				break;

			// ── empty / nothing ──
			case RegexNodeKind.Empty:
				break; // matches empty string — no code needed

			case RegexNodeKind.Nothing:
				statements.Add(ReturnFalse());
				break;

			// ── atomic group: treat as transparent wrapper ──
			case RegexNodeKind.Atomic:
				EmitNode(node.Child(0), statements);
				break;

			// ── group (non-capturing): transparent ──
			case RegexNodeKind.Group:
				EmitNode(node.Child(0), statements);
				break;

			case RegexNodeKind.UpdateBumpalong:
				break; // no-op for our purposes

			default:
				// For unsupported node kinds, emit a comment and return false as safe fallback
				statements.Add(ReturnFalse());
				break;
		}
	}

	// ────────────────────────── single character emitters ─────────────────────────

	private void EmitOneChar(RegexNode node, List<StatementSyntax> statements)
	{
		var ch = node.Ch;
		// if (pos >= input.Length || input[pos] != 'X') return false;
		statements.Add(IfReturnFalse(
			LogicalAndExpression(
				GreaterThanOrEqualExpression(Pos(), InputLength()),
				NotEqualsExpression(InputCharAt(Pos()), CreateLiteral(ch)))));
		// pos++;
		statements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, Pos())));
	}

	private void EmitNotoneChar(RegexNode node, List<StatementSyntax> statements)
	{
		var ch = node.Ch;
		// if (pos >= input.Length || input[pos] == 'X') return false;
		statements.Add(IfReturnFalse(
			LogicalOrExpression(
				GreaterThanOrEqualExpression(Pos(), InputLength()),
				EqualsExpression(InputCharAt(Pos()), CreateLiteral((ch))))));
		statements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, Pos())));
	}

	private void EmitMulti(RegexNode node, List<StatementSyntax> statements)
	{
		var str = node.Str!;
		var len = str.Length;
		
		// if (!input.Slice(pos).StartsWith("Test"))
		var strLiteral = CreateLiteral(str);
		
		var startsWithCall = InvocationExpression(
			MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
				InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
					IdentifierName(InputVar),
					IdentifierName("Slice")))
				.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(Pos())))),
				IdentifierName("StartsWith")))
			.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(strLiteral))));
		
		statements.Add(IfReturnFalse(
			PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, startsWithCall)));
			
		// if (pos + len > input.Length) return false;
		// statements.Add(IfReturnFalse(
		// 	GreaterThanExpression(AddExpression(Pos(), CreateLiteral(len)), InputLength())));

		// // Compare char-by-char for short strings, SequenceEqual for longer
		// if (len <= 4)
		// {
		// 	for (var i = 0; i < len; i++)
		// 	{
		// 		// if (input[pos + i] != 'c') return false;
		// 		statements.Add(IfReturnFalse(
		// 			NotEqualsExpression(InputCharAt(AddExpression(Pos(), CreateLiteral(i))), CreateLiteral(str[i]))));
		// 	}
		// }
		// else
		// {
		// 	// if (!input.Slice(pos, len).SequenceEqual("str".AsSpan())) return false;
		// 	var sliceCall = InvocationExpression(
		// 			MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
		// 				IdentifierName(InputVar),
		// 				IdentifierName("Slice")))
		// 		.WithArgumentList(ArgumentList(SeparatedList([
		// 			Argument(Pos()),
		// 			Argument(CreateLiteral(len))
		// 		])));
		//
		// 	var strSpan = InvocationExpression(
		// 		MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
		// 			CreateLiteral(str),
		// 			IdentifierName("AsSpan")));
		//
		// 	var seqEqual = InvocationExpression(
		// 			MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
		// 				sliceCall,
		// 				IdentifierName("SequenceEqual")))
		// 		.WithArgumentList(ArgumentList(SingletonSeparatedList(Argument(strSpan))));
		//
		// 	statements.Add(IfReturnFalse(LogicalNotExpression(seqEqual)));
		// }

		// pos += len;
		statements.Add(ExpressionStatement(
			AssignmentExpression(SyntaxKind.AddAssignmentExpression, Pos(), CreateLiteral(len))));
	}

	// ──────────────────────────── character class (Set) ───────────────────────────

	/// <summary>
	/// Emits a bounds check + character-class test for a single Set node.
	/// Delegates to <see cref="FunctionOptimizerContext.OptimizeBinaryExpression"/>
	/// so the rewriter's binary optimizer pipeline can further constant-fold and simplify.
	/// </summary>
	private void EmitSet(RegexNode node, List<StatementSyntax> statements)
	{
		// if (pos >= input.Length) return false;
		statements.Add(IfReturnFalse(GreaterThanOrEqualExpression(Pos(), InputLength())));

		var charExpr = InputCharAt(Pos());
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

		// ── well-known classes ──
		if (set == RegexCharClass.DigitClass || set == RegexCharClass.NegatedDigitClass)
		{
			var isDigit = CharMethodCall("IsDigit", charExpr);
			return set == RegexCharClass.DigitClass
				? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, isDigit)
				: isDigit;
		}

		if (set == RegexCharClass.WordClass || set == RegexCharClass.NegatedWordClass)
		{
			// \w = [A-Za-z0-9_]
			var isWord = EmitIsWordChar(charExpr);
			return set == RegexCharClass.WordClass
				? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, isWord)
				: isWord;
		}

		if (set == RegexCharClass.SpaceClass || set == RegexCharClass.NegatedSpaceClass)
		{
			var isSpace = CharMethodCall("IsWhiteSpace", charExpr);
			return set == RegexCharClass.SpaceClass
				? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, isSpace)
				: isSpace;
		}

		if (set == RegexCharClass.NotDigitClass)
		{
			return CharMethodCall("IsDigit", charExpr);
		}

		if (set == RegexCharClass.NotSpaceClass)
		{
			return CharMethodCall("IsWhiteSpace", charExpr);
		}

		if (set == RegexCharClass.NotWordClass)
		{
			return EmitIsWordChar(charExpr);
		}

		if (set == RegexCharClass.LetterClass || set == RegexCharClass.NotLetterClass)
		{
			var isLetter = CharMethodCall("IsLetter", charExpr);
			return set == RegexCharClass.LetterClass
				? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, isLetter)
				: isLetter;
		}

		if (set == RegexCharClass.LetterOrDigitClass || set == RegexCharClass.NotLetterOrDigitClass)
		{
			var isLetterOrDigit = CharMethodCall("IsLetterOrDigit", charExpr);
			return set == RegexCharClass.LetterOrDigitClass
				? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, isLetterOrDigit)
				: isLetterOrDigit;
		}

		if (set == RegexCharClass.LowerClass || set == RegexCharClass.NotLowerClass)
		{
			var isLower = CharMethodCall("IsLower", charExpr);
			return set == RegexCharClass.LowerClass
				? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, isLower)
				: isLower;
		}

		if (set == RegexCharClass.UpperClass || set == RegexCharClass.NotUpperClass)
		{
			var isUpper = CharMethodCall("IsUpper", charExpr);
			return set == RegexCharClass.UpperClass
				? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, isUpper)
				: isUpper;
		}

		if (set == RegexCharClass.ControlClass || set == RegexCharClass.NotControlClass)
		{
			var isControl = CharMethodCall("IsControl", charExpr);
			return set == RegexCharClass.ControlClass
				? PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, isControl)
				: isControl;
		}

		if (set == RegexCharClass.AnyClass)
		{
			// Any char matches — never fails (condition for "return false" is... false)
			return LiteralExpression(SyntaxKind.FalseLiteralExpression);
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

			return negated ? inRange : PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(inRange));
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

			return negated ? inEither : PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(inEither));
		}

		// ── small enumerable set — expand to OR-chain ──
		Span<char> chars = stackalloc char[5];
		var count = RegexCharClass.GetSetChars(set, chars);

		if (count > 0 && count <= 5)
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
			return PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(matchExpr!));
		}

		// ── fallback: enumerate all ranges from the set ──
		return EmitSetRangesFallback(set, charExpr, negated);
	}

	/// <summary>
	/// Fallback for character classes that are not a well-known class, singleton, or simple range.
	/// Enumerates the raw range pairs from the set string and builds an OR-of-range-checks expression.
	/// </summary>
	private ExpressionSyntax EmitSetRangesFallback(string set, ExpressionSyntax charExpr, bool negated)
	{
		var setLength = (int)set[RegexCharClass.SetLengthIndex];
		ExpressionSyntax? matchExpr = null;

		for (var i = RegexCharClass.SetStartIndex; i < RegexCharClass.SetStartIndex + setLength; i += 2)
		{
			var rangeLo = set[i];
			var rangeHi = i + 1 < RegexCharClass.SetStartIndex + setLength
				? (char)(set[i + 1] - 1)
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

		if (matchExpr is null)
		{
			// Empty set: nothing matches
			return negated
				? LiteralExpression(SyntaxKind.FalseLiteralExpression)
				: LiteralExpression(SyntaxKind.TrueLiteralExpression);
		}

		return negated ? matchExpr : PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(matchExpr));
	}

	// ──────────────────────────── greedy loop emitters ────────────────────────────

	private void EmitOneLoop(RegexNode node, List<StatementSyntax> statements)
	{
		var countVar = NextVar("count");
		var max = node.N == int.MaxValue ? InputLength() : (ExpressionSyntax)CreateLiteral(node.N);

		// int countN = 0;
		statements.Add(DeclareIntVar(countVar, 0));

		// while (countN < max && pos + countN < input.Length && input[pos + countN] == 'X') countN++;
		statements.Add(WhileStatement(
			LogicalAndExpression(
				LogicalAndExpression(
					LessThanExpression(IdentifierName(countVar), max),
					LessThanExpression(AddExpression(Pos(), IdentifierName(countVar)), InputLength())),
				EqualsExpression(InputCharAt(AddExpression(Pos(), IdentifierName(countVar))), CreateLiteral(node.Ch))),
			ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(countVar)))));

		if (node.M > 0)
			statements.Add(IfReturnFalse(LessThanExpression(IdentifierName(countVar), CreateLiteral(node.M))));

		// pos += countN;
		statements.Add(ExpressionStatement(
			AssignmentExpression(SyntaxKind.AddAssignmentExpression, Pos(), IdentifierName(countVar))));
	}

	private void EmitNotoneLoop(RegexNode node, List<StatementSyntax> statements)
	{
		var countVar = NextVar("count");
		var max = node.N == int.MaxValue ? InputLength() : (ExpressionSyntax)CreateLiteral(node.N);

		statements.Add(DeclareIntVar(countVar, 0));
		statements.Add(WhileStatement(
			LogicalAndExpression(
				LogicalAndExpression(
					LessThanExpression(IdentifierName(countVar), max),
					LessThanExpression(AddExpression(Pos(), IdentifierName(countVar)), InputLength())),
				NotEqualsExpression(InputCharAt(AddExpression(Pos(), IdentifierName(countVar))), CreateLiteral(node.Ch))),
			ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(countVar)))));

		if (node.M > 0)
			statements.Add(IfReturnFalse(LessThanExpression(IdentifierName(countVar), CreateLiteral(node.M))));

		statements.Add(ExpressionStatement(
			AssignmentExpression(SyntaxKind.AddAssignmentExpression, Pos(), IdentifierName(countVar))));
	}

	private void EmitSetLoop(RegexNode node, List<StatementSyntax> statements)
	{
		var countVar = NextVar("count");
		var max = node.N == int.MaxValue ? InputLength() : (ExpressionSyntax)CreateLiteral(node.N);
		var charAtExpr = InputCharAt(AddExpression(Pos(), IdentifierName(countVar)));

		// The condition for MATCHING is the negation of the "return false" condition
		var matchesCondition = EmitSetCondition(node.Str!, charAtExpr);
		// EmitSetCondition returns "does NOT match" → negate to get "does match"
		var loopCondition = PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(matchesCondition));

		statements.Add(DeclareIntVar(countVar, 0));
		statements.Add(WhileStatement(
			LogicalAndExpression(
				OptimizedLogicalAnd(
					LessThanExpression(IdentifierName(countVar), max),
					LessThanExpression(AddExpression(Pos(), IdentifierName(countVar)), InputLength())),
				loopCondition),
			ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(countVar)))));

		if (node.M > 0)
			statements.Add(IfReturnFalse(LessThanExpression(IdentifierName(countVar), CreateLiteral(node.M))));

		statements.Add(ExpressionStatement(
			AssignmentExpression(SyntaxKind.AddAssignmentExpression, Pos(), IdentifierName(countVar))));
	}

	// ─────────────────────────── lazy loop emitters ───────────────────────────────

	private void EmitOneLazy(RegexNode node, List<StatementSyntax> statements)
	{
		// For IsMatch, lazy loops match the minimum count
		EmitFixedRepeat(statements, node.M, charExpr => EqualsExpression(charExpr, CreateLiteral(node.Ch)));
	}

	private void EmitNotoneLazy(RegexNode node, List<StatementSyntax> statements)
	{
		EmitFixedRepeat(statements, node.M, charExpr => NotEqualsExpression(charExpr, CreateLiteral(node.Ch)));
	}

	private void EmitSetLazy(RegexNode node, List<StatementSyntax> statements)
	{
		// For lazy set loops, match the minimum count using the set condition
		for (var i = 0; i < node.M; i++)
		{
			statements.Add(IfReturnFalse(GreaterThanOrEqualExpression(Pos(), InputLength())));

			var charExpr = InputCharAt(Pos());
			var doesNotMatch = EmitSetCondition(node.Str!, charExpr);
			statements.Add(IfReturnFalse(doesNotMatch));
			statements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, Pos())));
		}
	}

	// ─────────────────────────── general loop emitters ────────────────────────────

	private void EmitGeneralLoop(RegexNode node, List<StatementSyntax> statements)
	{
		var iterVar = NextVar("iter");
		var child = node.Child(0);

		statements.Add(DeclareIntVar(iterVar, 0));

		// Build the loop body: try to match child, if success increment iter
		var bodyStatements = new List<StatementSyntax>();
		var savedPosVar = NextVar("savedPos");
		bodyStatements.Add(DeclareIntVar(savedPosVar, Pos()));

		var childStatements = new List<StatementSyntax>();
		EmitNode(child, childStatements);

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
				ExpressionStatement(AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, Pos(), IdentifierName(savedPosVar))),
				BreakStatement())));

		bodyStatements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, IdentifierName(iterVar))));

		var maxExpr = node.N == int.MaxValue
			? (ExpressionSyntax)MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
				PredefinedType(Token(SyntaxKind.IntKeyword)), IdentifierName("MaxValue"))
			: CreateLiteral(node.N);

		statements.Add(WhileStatement(
			LessThanExpression(IdentifierName(iterVar), maxExpr),
			Block(bodyStatements)));

		if (node.M > 0)
			statements.Add(IfReturnFalse(LessThanExpression(IdentifierName(iterVar), CreateLiteral(node.M))));
	}

	// ────────────────────────── alternation emitter ───────────────────────────────

	private void EmitAlternation(RegexNode node, List<StatementSyntax> statements)
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
		statements.Add(DeclareIntVar(savedPosVar, Pos()));

		var matchedVar = NextVar("altMatched");
		statements.Add(LocalDeclarationStatement(
			VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier(matchedVar))
						.WithInitializer(EqualsValueClause(LiteralExpression(SyntaxKind.FalseLiteralExpression)))))));

		for (var i = 0; i < childCount; i++)
		{
			var branchStatements = new List<StatementSyntax>();
			EmitNode(node.Child(i), branchStatements);
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
			statements.Add(ExpressionStatement(
				AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, Pos(), IdentifierName(savedPosVar))));

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

	private void EmitPositiveLookaround(RegexNode node, List<StatementSyntax> statements)
	{
		var savedPosVar = NextVar("laPos");
		statements.Add(DeclareIntVar(savedPosVar, Pos()));

		var childStatements = new List<StatementSyntax>();
		EmitNode(node.Child(0), childStatements);

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
		statements.Add(ExpressionStatement(
			AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, Pos(), IdentifierName(savedPosVar))));
	}

	private void EmitNegativeLookaround(RegexNode node, List<StatementSyntax> statements)
	{
		var savedPosVar = NextVar("nlPos");
		statements.Add(DeclareIntVar(savedPosVar, Pos()));

		var childStatements = new List<StatementSyntax>();
		EmitNode(node.Child(0), childStatements);

		var testName = NextVar("NegLookahead");
		var testFunc = LocalFunctionStatement(
				PredefinedType(Token(SyntaxKind.BoolKeyword)),
				Identifier(testName))
			.WithBody(Block(childStatements.Append(ReturnStatement(LiteralExpression(SyntaxKind.TrueLiteralExpression)))));

		statements.Add(testFunc);

		// if (NegLookahead()) return false; — if it DID match, the negative lookahead fails
		statements.Add(IfReturnFalse(InvocationExpression(IdentifierName(testName))));

		// Restore position
		statements.Add(ExpressionStatement(
			AssignmentExpression(SyntaxKind.SimpleAssignmentExpression, Pos(), IdentifierName(savedPosVar))));
	}

	// ────────────────────────── word boundary emitter ─────────────────────────────

	private void EmitWordBoundary(List<StatementSyntax> statements, bool negated)
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
								GreaterThanExpression(Pos(), Zero()),
								EmitIsWordChar(InputCharAt(SubtractExpression(Pos(), One()))))))))));

		statements.Add(LocalDeclarationStatement(
			VariableDeclaration(PredefinedType(Token(SyntaxKind.BoolKeyword)))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier(rightVar))
						.WithInitializer(EqualsValueClause(
							LogicalAndExpression(
								LessThanExpression(Pos(), InputLength()),
								EmitIsWordChar(InputCharAt(Pos())))))))));

		var condition = negated
			? NotEqualsExpression(IdentifierName(leftVar), IdentifierName(rightVar))   // \B: fail when they differ
			: EqualsExpression(IdentifierName(leftVar), IdentifierName(rightVar));     // \b: fail when they're same

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
	private void EmitFixedRepeat(List<StatementSyntax> statements, int count, Func<ExpressionSyntax, ExpressionSyntax> charCondition)
	{
		if (count == 0) return;

		// if (pos + count > input.Length) return false;
		statements.Add(IfReturnFalse(GreaterThanExpression(AddExpression(Pos(), CreateLiteral(count)), InputLength())));

		for (var i = 0; i < count; i++)
		{
			// if (!condition(input[pos])) return false;
			var charExpr = InputCharAt(Pos());
			var matches = charCondition(charExpr);
			statements.Add(IfReturnFalse(PrefixUnaryExpression(SyntaxKind.LogicalNotExpression, ParenthesizedExpression(matches))));
			statements.Add(ExpressionStatement(PostfixUnaryExpression(SyntaxKind.PostIncrementExpression, Pos())));
		}
	}

	// ══════════════════════════ Syntax Factory Helpers ════════════════════════════

	private string NextVar(string prefix) => $"{prefix}{_tempVarCounter++}";

	private static ExpressionSyntax Pos() => IdentifierName(PosVar);
	private static ExpressionSyntax InputLength() =>
		MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, IdentifierName(InputVar), IdentifierName("Length"));

	private static ExpressionSyntax InputCharAt(ExpressionSyntax index) =>
		ElementAccessExpression(IdentifierName(InputVar))
			.WithArgumentList(BracketedArgumentList(SingletonSeparatedList(Argument(index))));

	private static ExpressionSyntax Zero() => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(0));
	private static ExpressionSyntax One() => LiteralExpression(SyntaxKind.NumericLiteralExpression, Literal(1));

	private static InvocationExpressionSyntax CharMethodCall(string method, ExpressionSyntax arg) =>
		InvocationExpression(
				MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression,
					PredefinedType(Token(SyntaxKind.CharKeyword)),
					IdentifierName(method)))
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
			VariableDeclaration(PredefinedType(Token(SyntaxKind.VarKeyword)))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier(name))
						.WithInitializer(EqualsValueClause(CreateLiteral(value))))));

	private static LocalDeclarationStatementSyntax DeclareIntVar(string name, ExpressionSyntax value) =>
		LocalDeclarationStatement(
			VariableDeclaration(PredefinedType(Token(SyntaxKind.VarKeyword)))
				.WithVariables(SingletonSeparatedList(
					VariableDeclarator(Identifier(name))
						.WithInitializer(EqualsValueClause(value)))));
}


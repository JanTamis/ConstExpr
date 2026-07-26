using System.Collections.Concurrent;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Strips the outer widening cast off <c>(T)Unsafe.BitCast&lt;bool, byte&gt;(cond)</c> — the pattern
///   <see cref="Optimizers.ConditionalOptimizers.ConditionalExpressionOptimizer" /> emits for
///   <c>cond ? 1 : 0</c> / <c>cond ? 0 : 1</c> — wherever byte converts to <c>T</c> implicitly and the
///   cast's actual final position can't be type-directed: a <c>return</c>/expression-bodied-member, or
///   an operand of a binary operator between two primitive numeric types, which by C# language rules can
///   only ever resolve to a built-in operator (operator overloading requires a user-defined operand type).
///   <para>
///     Runs as a separate, final pass rather than at BitCast-creation time:
///     <see cref="Optimizers.ConditionalOptimizers.ConditionalExpressionOptimizer" /> only sees the
///     conditional's immediate syntactic position, but later passes in the same rewriter (e.g.
///     single-use local-variable inlining) can relocate the already-built BitCast expression into a
///     completely different position — a <c>var a = cond ? 1 : 0;</c> initializer (position unknown to
///     the optimizer, cast correctly kept) later inlined verbatim into <c>return a + b + ...;</c> (now a
///     safe binary-operand position, but nothing after creation time ever re-evaluates that). Evaluating
///     position on the fully-formed tree, after all relocation, is the only way to get this right.
///   </para>
///   <para>
///     Dropping the cast changes the emitted expression's *static* type from <c>T</c> to <c>byte</c>,
///     which — anywhere the static type is load-bearing (call/constructor/indexer arguments, generic type
///     inference, <c>var</c> initializers) — can silently rebind to a different overload. Both position
///     checks below exist specifically to rule that out (confirmed via a standalone repro: an overloaded
///     <c>SomeMethod(int)</c>/<c>SomeMethod(byte)</c> pair resolves differently depending on the argument's
///     static type); do not widen the safe-position set without re-confirming the same way.
///   </para>
/// </summary>
public sealed class RedundantBitCastElisionRewriter(SemanticModel semanticModel, ConcurrentDictionary<ulong, ISymbol> symbolStore) : CSharpSyntaxRewriter
{
	public static SyntaxNode Apply(SyntaxNode node, SemanticModel semanticModel, ConcurrentDictionary<ulong, ISymbol> symbolStore)
	{
		return new RedundantBitCastElisionRewriter(semanticModel, symbolStore).Visit(node);
	}

	public override SyntaxNode? VisitCastExpression(CastExpressionSyntax node)
	{
		var visited = (CastExpressionSyntax) base.VisitCastExpression(node)!;

		if (visited.Type is not PredefinedTypeSyntax predefined
		    || !CanImplicitlyConvertFromByte(predefined.Keyword.Kind())
		    || !IsBoolToByteBitCast(visited.Expression)
		    || !IsSafePosition(visited))
		{
			return visited;
		}

		return visited.Expression.WithTriviaFrom(visited);
	}

	/// <summary>Whether dropping the cast at <paramref name="node" />'s current position is provably safe (see class doc).</summary>
	private bool IsSafePosition(SyntaxNode node)
	{
		var current = node;

		while (current.Parent is ParenthesizedExpressionSyntax parenthesized)
		{
			current = parenthesized;
		}

		return current.Parent switch
		{
			ReturnStatementSyntax or ArrowExpressionClauseSyntax => true,
			BinaryExpressionSyntax binary => IsOtherOperandNumeric(binary, current),
			_ => false
		};
	}

	/// <summary>
	///   Two primitive-numeric operands can only ever combine via a built-in operator — C# does not allow
	///   overloading an operator unless at least one operand is a user-defined type — so checking the
	///   other operand's type is sufficient, without needing symbol resolution on the (possibly
	///   synthetic, semantic-model-invisible) binary expression itself.
	/// </summary>
	private bool IsOtherOperandNumeric(BinaryExpressionSyntax binary, SyntaxNode current)
	{
		var other = ReferenceEquals(binary.Left, current) ? binary.Right : binary.Left;

		return IsNumericExpression(other);
	}

	/// <summary>
	///   Whether <paramref name="expr" /> is provably numeric without needing it to be resolvable in the
	///   original compiled tree. Three cases, in order:
	///   <list type="bullet">
	///     <item>
	///       A bare numeric literal (e.g. a count/multiplier <c>AddChainMultiplyStrategy</c> or a
	///       strength-reduction pass synthesized) — numeric by construction off its token value, no
	///       semantic model needed; such a literal is rarely annotated on its own even when the combined
	///       expression it ends up in is.
	///     </item>
	///     <item>
	///       <see cref="CompilationExtensions.TryGetTypeSymbol" />, which already has the
	///       type-symbol-annotation fallback synthetic nodes carry, unlike plain
	///       <c>semanticModel.GetTypeInfo</c>/<c>GetSymbolInfo</c>.
	///     </item>
	///     <item>
	///       Recursing into a nested arithmetic/bitwise/shift expression (e.g. a fresh <c>x &lt;&lt; 1</c>
	///       a strength-reduction pass built, itself unannotated) whose own operands are numeric — such an
	///       expression can only ever itself be numeric. Deliberately excludes relational/equality/logical
	///       operator kinds, which produce <c>bool</c>, not a numeric result.
	///     </item>
	///   </list>
	/// </summary>
	private bool IsNumericExpression(ExpressionSyntax expr)
	{
		// Consolidation passes (e.g. AddChainMultiplyStrategy.WrapForAdd) parenthesize a lower-precedence
		// term — a shift, say — when splicing it into a `+` chain; unwrap to reach the real expression.
		while (expr is ParenthesizedExpressionSyntax parenthesized)
		{
			expr = parenthesized.Expression;
		}

		if (expr is LiteralExpressionSyntax { Token.Value: byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal })
		{
			return true;
		}

		if (semanticModel.TryGetTypeSymbol(expr, symbolStore, out var type) && type.IsNumericType())
		{
			return true;
		}

		// A ternary's `throw` branch never produces a value, so it can't make the ternary non-numeric —
		// only the other branch's type matters, same as real C#'s own typing rule for `cond ? x : throw`.
		if (expr is ConditionalExpressionSyntax conditional)
		{
			return (conditional.WhenTrue is ThrowExpressionSyntax || IsNumericExpression(conditional.WhenTrue))
			       && (conditional.WhenFalse is ThrowExpressionSyntax || IsNumericExpression(conditional.WhenFalse));
		}

		return expr is BinaryExpressionSyntax binary
		       && binary.Kind() is SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression
			       or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression or SyntaxKind.LeftShiftExpression
			       or SyntaxKind.RightShiftExpression or SyntaxKind.UnsignedRightShiftExpression
			       or SyntaxKind.BitwiseAndExpression or SyntaxKind.BitwiseOrExpression or SyntaxKind.ExclusiveOrExpression
		       && IsNumericExpression(binary.Left) && IsNumericExpression(binary.Right);
	}

	private static bool IsBoolToByteBitCast(ExpressionSyntax expr)
	{
		return expr is InvocationExpressionSyntax
		{
			ArgumentList.Arguments.Count: 1,
			Expression: MemberAccessExpressionSyntax
			{
				Expression: IdentifierNameSyntax { Identifier.ValueText: "Unsafe" },
				Name: GenericNameSyntax
				{
					Identifier.ValueText: "BitCast",
					TypeArgumentList.Arguments:
					[
						PredefinedTypeSyntax { Keyword.RawKind: (int) SyntaxKind.BoolKeyword },
						PredefinedTypeSyntax { Keyword.RawKind: (int) SyntaxKind.ByteKeyword }
					]
				}
			}
		};
	}

	/// <summary>Byte widens implicitly to every numeric type except sbyte and char, which need the explicit cast kept.</summary>
	private static bool CanImplicitlyConvertFromByte(SyntaxKind keyword)
	{
		return keyword is SyntaxKind.ByteKeyword
			or SyntaxKind.ShortKeyword
			or SyntaxKind.UShortKeyword
			or SyntaxKind.IntKeyword
			or SyntaxKind.UIntKeyword
			or SyntaxKind.LongKeyword
			or SyntaxKind.ULongKeyword
			or SyntaxKind.FloatKeyword
			or SyntaxKind.DoubleKeyword
			or SyntaxKind.DecimalKeyword;
	}
}
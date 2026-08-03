using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   An inclusive integer interval <c>[Min, Max]</c> that a value is known to fall in.
/// </summary>
internal readonly record struct ValueRange(long Min, long Max)
{
	public static ValueRange Exact(long value)
	{
		return new ValueRange(value, value);
	}

	/// <summary>A range that pins the value down completely, so it can act as a bound elsewhere.</summary>
	public bool IsSinglePoint => Min == Max;

	public bool Overlaps(ValueRange other)
	{
		return Min <= other.Max && other.Min <= Max;
	}

	/// <summary>Tightens this range with another. <c>null</c> when the two contradict each other.</summary>
	public ValueRange? Intersect(ValueRange other)
	{
		var min = Math.Max(Min, other.Min);
		var max = Math.Min(Max, other.Max);

		return min <= max ? new ValueRange(min, max) : null;
	}
}

/// <summary>
///   Derives the interval an integer expression is known to fall in, so that a comparison whose
///   outcome is already settled can be folded away by <see cref="ValueRangeRewriter" />:
///   <code>
///   for (var i = 0; i &lt; 8; i++)
///       if (i &lt; 10) …        =>   the guard is always true and disappears
/// 
///   if ((hash &amp; 15) > 20) …   =>   the mask caps the value at 15; always false
///   </code>
///   <para>
///     Facts come from integer literals, an ascending unit-step <c>for</c> header, and the condition
///     of an enclosing <c>if</c>/<c>while</c> on the branch that was taken. They are combined with
///     interval arithmetic. Anything not covered yields <c>null</c> — no range, no fold.
///   </para>
///   <para>
///     Two rules keep this sound without a semantic model, which no longer covers the rewritten tree
///     by the time the pass runs. First, a fact is discarded the moment the value could have been
///     reassigned between the fact and the use (see <see cref="FromFacts" />). Second, every
///     <em>computed</em> range must come out non-negative: an unsigned subtraction wraps to a huge
///     value instead of going negative, and the declared type is unknowable here, so a negative
///     computed interval is refused rather than guessed at. Literals and refinements, which mirror a
///     comparison the code itself performs, may be negative.
///   </para>
/// </summary>
/// <remarks>
///   ponytail: local syntactic analysis, no SSA and no fixpoint, and no symbolic facts about
///   <c>arr.Length</c>. It covers the shapes above and bails to null everywhere else. Upgrade to real
///   dataflow only once the bail rate on the Sample methods measurably hurts.
/// </remarks>
internal static class ValueRangeAnalysis
{
	/// <summary>
	///   Bounds the mutual recursion between <see cref="For" /> and <see cref="Refine" />: the bound of
	///   a comparison is itself analysed, and <c>if (i &lt; j) … if (j &lt; i) …</c> would otherwise
	///   chase itself forever.
	/// </summary>
	private const int MaxDepth = 4;

	/// <summary>
	///   The interval <paramref name="expression" /> is known to fall in, or <c>null</c> when nothing
	///   can be derived. <paramref name="scope" /> is the outermost node facts may be collected from —
	///   normally the method body the pass was handed.
	/// </summary>
	public static ValueRange? For(ExpressionSyntax expression, SyntaxNode scope)
	{
		return For(expression, scope, MaxDepth);
	}

	/// <summary>
	///   Whether <paramref name="kind" /> is settled for every pair of values the two ranges allow.
	///   <c>null</c> means both outcomes remain possible.
	/// </summary>
	public static bool? Decide(SyntaxKind kind, ValueRange left, ValueRange right)
	{
		switch (kind)
		{
			case SyntaxKind.LessThanExpression:
				return left.Max < right.Min ? true : left.Min >= right.Max ? false : null;

			case SyntaxKind.LessThanOrEqualExpression:
				return left.Max <= right.Min ? true : left.Min > right.Max ? false : null;

			case SyntaxKind.GreaterThanExpression:
				return left.Min > right.Max ? true : left.Max <= right.Min ? false : null;

			case SyntaxKind.GreaterThanOrEqualExpression:
				return left.Min >= right.Max ? true : left.Max < right.Min ? false : null;

			// Equality needs both sides pinned to the same single value to be certainly true, but only
			// needs the intervals to miss each other to be certainly false.
			case SyntaxKind.EqualsExpression:
				return left.IsSinglePoint && right.IsSinglePoint && left.Min == right.Min ? true
					: !left.Overlaps(right) ? false
					: null;

			case SyntaxKind.NotEqualsExpression:
				return left.IsSinglePoint && right.IsSinglePoint && left.Min == right.Min ? false
					: !left.Overlaps(right) ? true
					: null;

			default:
				return null;
		}
	}

	/// <summary>
	///   Whether <paramref name="pattern" /> is settled for every value <paramref name="subject" />
	///   allows. The conditional-and strategies fold a comparison chain into
	///   <c>
	///     x is &lt;= 15 and
	///     &lt; 8
	///   </c>
	///   before this pass runs, so most settled comparisons arrive in this form rather than
	///   as a binary expression.
	/// </summary>
	public static bool? Decide(PatternSyntax pattern, ValueRange subject, SyntaxNode scope)
	{
		switch (pattern)
		{
			case ParenthesizedPatternSyntax parenthesized:
				return Decide(parenthesized.Pattern, subject, scope);

			case UnaryPatternSyntax { RawKind: (int) SyntaxKind.NotPattern } negation:
				return Decide(negation.Pattern, subject, scope) is { } inner ? !inner : null;

			case BinaryPatternSyntax { RawKind: (int) SyntaxKind.AndPattern } conjunction:
			{
				var left = Decide(conjunction.Left, subject, scope);
				var right = Decide(conjunction.Right, subject, scope);

				return left == false || right == false ? false
					: left == true && right == true ? true
					: null;
			}

			case BinaryPatternSyntax { RawKind: (int) SyntaxKind.OrPattern } disjunction:
			{
				var left = Decide(disjunction.Left, subject, scope);
				var right = Decide(disjunction.Right, subject, scope);

				return left == true || right == true ? true
					: left == false && right == false ? false
					: null;
			}

			case ConstantPatternSyntax constant:
				return For(constant.Expression, scope, MaxDepth) is { } value ? Decide(SyntaxKind.EqualsExpression, subject, value) : null;

			case RelationalPatternSyntax relational:
				return RelationalKind(relational) is var kind && kind != SyntaxKind.None && For(relational.Expression, scope, MaxDepth) is { } bound
					? Decide(kind, subject, bound)
					: null;

			default:
				return null;
		}
	}

	/// <summary>
	///   Drops the conjuncts and disjuncts of <paramref name="pattern" /> that cannot change its
	///   outcome, returning the pattern itself when none can go. Only called once the pattern as a
	///   whole turned out <em>not</em> to be settled, so at least one branch always survives.
	/// </summary>
	public static PatternSyntax Reduce(PatternSyntax pattern, ValueRange subject, SyntaxNode scope)
	{
		switch (pattern)
		{
			// Kept rather than unwrapped: `and` binds tighter than `or`, so dropping the parentheses
			// here would silently reassociate the pattern.
			case ParenthesizedPatternSyntax parenthesized:
				return parenthesized.WithPattern(Reduce(parenthesized.Pattern, subject, scope));

			case BinaryPatternSyntax { RawKind: (int) SyntaxKind.AndPattern } conjunction:
				if (Decide(conjunction.Left, subject, scope) == true)
				{
					return Reduce(conjunction.Right, subject, scope).WithTriviaFrom(pattern);
				}

				return Decide(conjunction.Right, subject, scope) == true
					? Reduce(conjunction.Left, subject, scope).WithTriviaFrom(pattern)
					: conjunction.WithLeft(Reduce(conjunction.Left, subject, scope)).WithRight(Reduce(conjunction.Right, subject, scope));

			case BinaryPatternSyntax { RawKind: (int) SyntaxKind.OrPattern } disjunction:
				if (Decide(disjunction.Left, subject, scope) == false)
				{
					return Reduce(disjunction.Right, subject, scope).WithTriviaFrom(pattern);
				}

				return Decide(disjunction.Right, subject, scope) == false
					? Reduce(disjunction.Left, subject, scope).WithTriviaFrom(pattern)
					: disjunction.WithLeft(Reduce(disjunction.Left, subject, scope)).WithRight(Reduce(disjunction.Right, subject, scope));

			default:
				return pattern;
		}
	}

	public static bool IsComparison(SyntaxKind kind)
	{
		return kind is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
			or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression
			or SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression;
	}

	private static ValueRange? For(ExpressionSyntax expression, SyntaxNode scope, int depth)
	{
		if (depth <= 0)
		{
			return null;
		}

		switch (expression)
		{
			case ParenthesizedExpressionSyntax parenthesized:
				return For(parenthesized.Expression, scope, depth);

			case LiteralExpressionSyntax literal:
				return AsLong(literal.Token.Value) is { } value ? ValueRange.Exact(value) : null;

			// Only over a literal. `-x` on an unsigned operand wraps to a huge positive value rather
			// than going negative, and the declared type is not available here.
			case PrefixUnaryExpressionSyntax negation when negation.IsKind(SyntaxKind.UnaryMinusExpression):
				return negation.Operand is LiteralExpressionSyntax operand && AsLong(operand.Token.Value) is { } negated
					? ValueRange.Exact(-negated)
					: null;

			case PrefixUnaryExpressionSyntax plus when plus.IsKind(SyntaxKind.UnaryPlusExpression):
				return For(plus.Operand, scope, depth);

			case CastExpressionSyntax cast:
				return WithoutTruncation(cast.Type, For(cast.Expression, scope, depth));

			case BinaryExpressionSyntax binary:
				return NonNegative(Combine(binary.Kind(), For(binary.Left, scope, depth), For(binary.Right, scope, depth)));

			case IdentifierNameSyntax identifier:
				return FromFacts(identifier, scope, depth);

			default:
				return null;
		}
	}

	/// <summary>
	///   Walks out from a use of <paramref name="identifier" /> towards <paramref name="scope" />,
	///   collecting every fact the enclosing statements establish about it and intersecting them.
	///   <para>
	///     Two things invalidate a fact on the way up, and both must be handled or the pass folds a
	///     comparison that is not actually settled. Leaving a loop that writes the name: the fact held
	///     on entry, not on the iteration this use belongs to — which is also what stops
	///     <c>while (x &lt; 10)</c> under an enclosing <c>x &lt;= 4</c> from being folded into an
	///     infinite loop. And passing a preceding statement in the same block that writes the name: it
	///     replaced the value every outer fact was about.
	///   </para>
	/// </summary>
	private static ValueRange? FromFacts(IdentifierNameSyntax identifier, SyntaxNode scope, int depth)
	{
		var name = identifier.Identifier.Text;
		var child = (SyntaxNode) identifier;
		var killed = false;
		ValueRange? result = null;

		for (var ancestor = identifier.Parent; ancestor is not null; ancestor = ancestor.Parent)
		{
			switch (ancestor)
			{
				// The header that introduced the name. Nothing further out can describe it, so this is
				// the end of the walk either way. Note the body-write check: a counter the body assigns
				// to is no longer described by its header.
				case ForStatementSyntax loop when child == loop.Statement && DeclaresCounter(loop, name):
					return killed || Writes(loop.Statement, name) ? null : Narrow(result, CounterRange(loop, scope, depth));

				case IfStatementSyntax branch when !killed && (child == branch.Statement || child == branch.Else):
					result = Narrow(result, Refine(branch.Condition, name, child == branch.Statement, scope, depth));
					break;

				// The partial rewriter collapses most guards into a ternary long before this pass runs,
				// so this is the ordinary case rather than the exotic one.
				case ConditionalExpressionSyntax ternary when !killed && (child == ternary.WhenTrue || child == ternary.WhenFalse):
					result = Narrow(result, Refine(ternary.Condition, name, child == ternary.WhenTrue, scope, depth));
					break;

				// Short-circuiting makes a left operand a guard over the right one: `a && b` reaches b
				// only once a held, `a || b` only once it did not. This is where the second half of a
				// range check picks up what the first half established.
				case BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalAndExpression } conjunction when !killed && child == conjunction.Right:
					result = Narrow(result, Refine(conjunction.Left, name, true, scope, depth));
					break;

				case BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalOrExpression } disjunction when !killed && child == disjunction.Right:
					result = Narrow(result, Refine(disjunction.Left, name, false, scope, depth));
					break;

				// The condition holds at the top of every iteration, so only for a name the body leaves
				// alone. Reached only from the body — a use inside the condition itself is below.
				case WhileStatementSyntax loop when !killed && child == loop.Statement && !Writes(loop.Statement, name):
					result = Narrow(result, Refine(loop.Condition, name, true, scope, depth));
					break;

				// A declaration is where the name comes from, so its initializer has the last word and
				// the walk ends — same as a `for` counter reaching its header. The early-exit guards the
				// same scan collects apply either way, so they are folded in before that decision.
				case BlockSyntax block:
				{
					var definition = Preceding(block, child, name, ref killed, out var guards);

					// Subject to `killed` for the same reason the initializer below is, and it is an outer
					// block where that matters: a use inside a loop whose body writes the name is reached
					// again on an iteration the guard no longer describes. The loop-leaving check further
					// down has already set the flag by the time the walk gets out here.
					if (!killed)
					{
						result = Narrow(result, guards);
					}

					if (definition is not null)
					{
						return killed ? null : Narrow(result, For(definition, scope, depth - 1));
					}

					break;
				}
			}

			// Leaving a loop invalidates every fact about a name the loop writes: the fact held on
			// entry, not on the iteration this use belongs to. That is also what stops `while (x < 10)`
			// under an enclosing `x <= 4` from being folded into an infinite loop.
			if (ancestor is ForStatementSyntax or WhileStatementSyntax or DoStatementSyntax or ForEachStatementSyntax)
			{
				killed |= Writes(ancestor, name);
			}

			if (ancestor == scope)
			{
				break;
			}

			child = ancestor;
		}

		return result;
	}

	/// <summary>
	///   Scans the statements of <paramref name="block" /> that run before <paramref name="child" />,
	///   returning the initializer of the declaration that introduced <paramref name="name" /> if one
	///   is among them. Sets <paramref name="killed" /> when any of them can write to the name: that
	///   write replaced the value every outer fact — and any earlier declaration — was about.
	///   <para>
	///     <paramref name="guards" /> is what the early-exit guards among those statements leave behind:
	///     <c>if (c) return …;</c> means the fall-through only reaches <paramref name="child" /> once
	///     <c>c</c> did <em>not</em> hold, so the negated condition narrows the name exactly like an
	///     enclosing <c>if</c> would. Without this, the shape the generator emits itself goes unfolded —
	///     an early-exit range check followed by a second test of the same range, which is what
	///     <c>BinomialCoefficient</c> shows in the Sample output.
	///   </para>
	/// </summary>
	/// <remarks>
	///   ponytail: only <c>return</c>/<c>throw</c> count as exits, and only an <c>if</c> — not a
	///   <c>switch</c>. <c>break</c>/<c>continue</c>/<c>goto</c> leave the innermost loop or jump to a
	///   label, which is a dominance question this syntactic outward walk cannot answer; and a
	///   <c>switch</c> whose sections happen to return (see <c>BinomialCoefficient</c>'s other overload)
	///   looks like an early exit but leaves "no section matched", which is no single interval. Widen
	///   only if the emitted code starts asking for it.
	/// </remarks>
	private static ExpressionSyntax? Preceding(BlockSyntax block, SyntaxNode child, string name, ref bool killed, out ValueRange? guards)
	{
		ExpressionSyntax? definition = null;

		guards = null;

		foreach (var statement in block.Statements)
		{
			if (statement == child)
			{
				break;
			}

			if (statement is LocalDeclarationStatementSyntax { Declaration.Variables: [ { Initializer.Value: { } value } declarator ] }
			    && declarator.Identifier.Text == name)
			{
				definition = value;

				// A redeclaration is a different variable; nothing an earlier guard said still applies.
				guards = null;
				continue;
			}

			if (statement is IfStatementSyntax { Else: null } guard
			    && Exits(guard.Statement)
			    && LoopInvariance.IsPureExpression(guard.Condition))
			{
				guards = Narrow(guards, Refine(guard.Condition, name, false, block, MaxDepth));
			}

			// `killed` is one bit for the whole walk and has no per-statement granularity, but `guards`
			// needs it: a guard established before a write describes the value that write replaced. Hence
			// the reset here rather than a single check at the end.
			if (Writes(statement, name))
			{
				killed = true;
				guards = null;
			}
		}

		return definition;
	}

	/// <summary>
	///   Whether the branch of a guarding <c>if</c> leaves the enclosing block outright, so that anything
	///   after it is only reached when the condition did not hold.
	/// </summary>
	private static bool Exits(StatementSyntax statement)
	{
		return statement switch
		{
			ReturnStatementSyntax or ThrowStatementSyntax => true,
			BlockSyntax { Statements: [ ReturnStatementSyntax or ThrowStatementSyntax ] } => true,
			_ => false
		};
	}

	/// <summary>
	///   The range an ascending unit-step <c>for</c> counter takes inside the loop body. Anything else
	///   — a descending loop, a step other than one, a bound that is not itself analysable — bails.
	/// </summary>
	private static ValueRange? CounterRange(ForStatementSyntax loop, SyntaxNode scope, int depth)
	{
		if (loop.Declaration?.Variables is not [ { Initializer.Value: { } start } ]
		    || loop.Incrementors is not [ var incrementor ]
		    || !IsUnitIncrement(incrementor)
		    || loop.Condition is not BinaryExpressionSyntax condition
		    || condition.Left is not IdentifierNameSyntax)
		{
			return null;
		}

		if (For(start, scope, depth) is not { } from)
		{
			return null;
		}

		// An unanalysable bound still leaves the lower half: the counter starts at the initializer and
		// only ever goes up. `for (var i = 0; i < n; i++)` is the common shape, and knowing i >= 0 is
		// most of what is worth knowing about it.
		if (For(condition.Right, scope, depth) is not { } bound)
		{
			return condition.Kind() is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
				? new ValueRange(from.Min, Int64.MaxValue)
				: null;
		}

		// The weakest sound endpoints: the lowest the counter can start at, and the highest the bound
		// can let it reach.
		var last = condition.Kind() switch
		{
			SyntaxKind.LessThanExpression when bound.Max > Int64.MinValue => bound.Max - 1,
			SyntaxKind.LessThanOrEqualExpression => bound.Max,
			_ => (long?) null
		};

		// from.Min > last means the body never runs. Sound to claim anything there, but an empty
		// interval only invites confusing folds in unreachable code.
		return last is { } max && from.Min <= max ? new ValueRange(from.Min, max) : null;
	}

	private static bool DeclaresCounter(ForStatementSyntax loop, string name)
	{
		return loop.Declaration?.Variables is [ var declarator ] && declarator.Identifier.Text == name;
	}

	private static bool IsUnitIncrement(ExpressionSyntax incrementor)
	{
		return incrementor.IsKind(SyntaxKind.PostIncrementExpression)
		       || incrementor.IsKind(SyntaxKind.PreIncrementExpression)
		       || incrementor is AssignmentExpressionSyntax { RawKind: (int) SyntaxKind.AddAssignmentExpression, Right: LiteralExpressionSyntax { Token.Value: 1 } };
	}

	/// <summary>
	///   What <paramref name="condition" /> says about <paramref name="name" /> on the branch where it
	///   evaluates to <paramref name="positive" />.
	/// </summary>
	private static ValueRange? Refine(ExpressionSyntax condition, string name, bool positive, SyntaxNode scope, int depth)
	{
		switch (condition)
		{
			case ParenthesizedExpressionSyntax parenthesized:
				return Refine(parenthesized.Expression, name, positive, scope, depth);

			case PrefixUnaryExpressionSyntax negation when negation.IsKind(SyntaxKind.LogicalNotExpression):
				return Refine(negation.Operand, name, !positive, scope, depth);

			// Both halves of an `&&` hold on the true branch; by De Morgan both negated halves of an
			// `||` hold on the false branch. The other two combinations narrow nothing.
			case BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalAndExpression } conjunction when positive:
				return Narrow(Refine(conjunction.Left, name, true, scope, depth), Refine(conjunction.Right, name, true, scope, depth));

			case BinaryExpressionSyntax { RawKind: (int) SyntaxKind.LogicalOrExpression } disjunction when !positive:
				return Narrow(Refine(disjunction.Left, name, false, scope, depth), Refine(disjunction.Right, name, false, scope, depth));

			case BinaryExpressionSyntax comparison when IsComparison(comparison.Kind()):
				return RefineComparison(comparison, name, positive, scope, depth);

			case IsPatternExpressionSyntax pattern when IsNamed(pattern.Expression, name):
				return RefinePattern(pattern.Pattern, positive, scope, depth);

			default:
				return null;
		}
	}

	private static ValueRange? RefineComparison(BinaryExpressionSyntax comparison, string name, bool positive, SyntaxNode scope, int depth)
	{
		var kind = comparison.Kind();
		ExpressionSyntax bound;
		bool unsigned;

		if (Targets(comparison.Left, name, out unsigned))
		{
			bound = comparison.Right;
		}
		else if (Targets(comparison.Right, name, out unsigned))
		{
			bound = comparison.Left;
			kind = Mirror(kind);
		}
		else
		{
			return null;
		}

		kind = positive ? kind : Negate(kind);

		// Analysing the bound re-enters this walk, hence the depth step.
		if (For(bound, scope, depth - 1) is not { } range)
		{
			return null;
		}

		if (!unsigned)
		{
			return FromRelation(kind, range);
		}

		// `(uint) x <= 9` is how the conditional-and strategies spell `x >= 0 && x <= 9`: a negative x
		// casts to a huge unsigned value that fails the test, so the check carries a lower bound of
		// zero along with the upper one. Only that direction transfers — `(uint) x >= 9` holds for
		// every negative x and so says nothing at all about a signed one.
		return kind is SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression or SyntaxKind.EqualsExpression
			? Narrow(FromRelation(kind, range), new ValueRange(0, Int64.MaxValue))
			: null;
	}

	/// <summary>
	///   Whether the operand is the name being refined, possibly behind the cast to an unsigned type
	///   that turns a two-sided range check into a single comparison.
	/// </summary>
	private static bool Targets(ExpressionSyntax operand, string name, out bool unsigned)
	{
		if (operand is CastExpressionSyntax { Type: PredefinedTypeSyntax { RawKind: (int) SyntaxKind.PredefinedType } keyword } cast
		    && keyword.Keyword.Kind() is SyntaxKind.UIntKeyword or SyntaxKind.ULongKeyword)
		{
			unsigned = true;

			return IsNamed(cast.Expression, name);
		}

		unsigned = false;

		return IsNamed(operand, name);
	}

	private static ValueRange? RefinePattern(PatternSyntax pattern, bool positive, SyntaxNode scope, int depth)
	{
		switch (pattern)
		{
			case ParenthesizedPatternSyntax parenthesized:
				return RefinePattern(parenthesized.Pattern, positive, scope, depth);

			case UnaryPatternSyntax { RawKind: (int) SyntaxKind.NotPattern } negation:
				return RefinePattern(negation.Pattern, !positive, scope, depth);

			case BinaryPatternSyntax { RawKind: (int) SyntaxKind.AndPattern } conjunction when positive:
				return Narrow(RefinePattern(conjunction.Left, true, scope, depth), RefinePattern(conjunction.Right, true, scope, depth));

			case BinaryPatternSyntax { RawKind: (int) SyntaxKind.OrPattern } disjunction when !positive:
				return Narrow(RefinePattern(disjunction.Left, false, scope, depth), RefinePattern(disjunction.Right, false, scope, depth));

			case ConstantPatternSyntax constant when positive:
				return For(constant.Expression, scope, depth - 1);

			case RelationalPatternSyntax relational:
				return RelationalKind(relational) is var kind && kind != SyntaxKind.None && For(relational.Expression, scope, depth - 1) is { } range
					? FromRelation(positive ? kind : Negate(kind), range)
					: null;

			default:
				return null;
		}
	}

	/// <summary>
	///   The comparison a relational pattern (<c>&lt; 8</c>) stands for, or
	///   <see cref="SyntaxKind.None" /> for an operator this analysis does not model.
	/// </summary>
	private static SyntaxKind RelationalKind(RelationalPatternSyntax pattern)
	{
		return pattern.OperatorToken.Kind() switch
		{
			SyntaxKind.LessThanToken => SyntaxKind.LessThanExpression,
			SyntaxKind.LessThanEqualsToken => SyntaxKind.LessThanOrEqualExpression,
			SyntaxKind.GreaterThanToken => SyntaxKind.GreaterThanExpression,
			SyntaxKind.GreaterThanEqualsToken => SyntaxKind.GreaterThanOrEqualExpression,
			_ => SyntaxKind.None
		};
	}

	/// <summary>
	///   The half-open interval implied by <c>value &lt;op&gt; bound</c>, using the loosest endpoint the
	///   bound's own range allows.
	/// </summary>
	private static ValueRange? FromRelation(SyntaxKind kind, ValueRange bound)
	{
		switch (kind)
		{
			case SyntaxKind.LessThanExpression:
				return bound.Max > Int64.MinValue ? new ValueRange(Int64.MinValue, bound.Max - 1) : null;

			case SyntaxKind.LessThanOrEqualExpression:
				return new ValueRange(Int64.MinValue, bound.Max);

			case SyntaxKind.GreaterThanExpression:
				return bound.Min < Int64.MaxValue ? new ValueRange(bound.Min + 1, Int64.MaxValue) : null;

			case SyntaxKind.GreaterThanOrEqualExpression:
				return new ValueRange(bound.Min, Int64.MaxValue);

			case SyntaxKind.EqualsExpression:
				return bound;

			// `!=` only excludes a hole in the middle, which an interval cannot express.
			default:
				return null;
		}
	}

	private static SyntaxKind Mirror(SyntaxKind kind)
	{
		return kind switch
		{
			SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanExpression,
			SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanOrEqualExpression,
			SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanExpression,
			SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanOrEqualExpression,
			_ => kind
		};
	}

	private static SyntaxKind Negate(SyntaxKind kind)
	{
		return kind switch
		{
			SyntaxKind.LessThanExpression => SyntaxKind.GreaterThanOrEqualExpression,
			SyntaxKind.LessThanOrEqualExpression => SyntaxKind.GreaterThanExpression,
			SyntaxKind.GreaterThanExpression => SyntaxKind.LessThanOrEqualExpression,
			SyntaxKind.GreaterThanOrEqualExpression => SyntaxKind.LessThanExpression,
			SyntaxKind.EqualsExpression => SyntaxKind.NotEqualsExpression,
			SyntaxKind.NotEqualsExpression => SyntaxKind.EqualsExpression,
			_ => SyntaxKind.None
		};
	}

	/// <summary>
	///   Interval arithmetic. Every operator that could produce a negative result from non-negative
	///   inputs, or that depends on the operand's signedness, is either restricted to a non-negative
	///   left side or left out; the <see cref="NonNegative" /> filter at the call site is the backstop.
	/// </summary>
	private static ValueRange? Combine(SyntaxKind kind, ValueRange? left, ValueRange? right)
	{
		// A mask with a non-negative constant caps the result whatever the other side holds — the one
		// case worth handling with only one side known, and the reason `hash & 15` yields [0, 15].
		if (kind == SyntaxKind.BitwiseAndExpression && (Constant(right) ?? Constant(left)) is { } mask)
		{
			return new ValueRange(0, mask);
		}

		if (left is not { } l || right is not { } r)
		{
			return null;
		}

		try
		{
			checked
			{
				switch (kind)
				{
					case SyntaxKind.AddExpression:
						return new ValueRange(l.Min + r.Min, l.Max + r.Max);

					case SyntaxKind.SubtractExpression:
						return new ValueRange(l.Min - r.Max, l.Max - r.Min);

					case SyntaxKind.MultiplyExpression:
						var corners = new[] { l.Min * r.Min, l.Min * r.Max, l.Max * r.Min, l.Max * r.Max };
						var low = corners[0];
						var high = corners[0];

						foreach (var corner in corners)
						{
							low = Math.Min(low, corner);
							high = Math.Max(high, corner);
						}

						return new ValueRange(low, high);

					// Division and remainder are only well behaved here for a non-negative dividend and a
					// positive constant divisor: C# truncates towards zero and keeps the dividend's sign.
					case SyntaxKind.DivideExpression when l.Min >= 0 && Constant(r) is { } divisor && divisor > 0:
						return new ValueRange(l.Min / divisor, l.Max / divisor);

					case SyntaxKind.ModuloExpression when l.Min >= 0 && Constant(r) is { } modulus && modulus > 0:
						return new ValueRange(0, Math.Min(l.Max, modulus - 1));

					case SyntaxKind.BitwiseAndExpression when l.Min >= 0 && r.Min >= 0:
						return new ValueRange(0, Math.Min(l.Max, r.Max));

					// For non-negative a, b: max(a, b) <= a | b <= a + b, and 0 <= a ^ b <= a + b.
					case SyntaxKind.BitwiseOrExpression when l.Min >= 0 && r.Min >= 0:
						return new ValueRange(Math.Max(l.Min, r.Min), l.Max + r.Max);

					case SyntaxKind.ExclusiveOrExpression when l.Min >= 0 && r.Min >= 0:
						return new ValueRange(0, l.Max + r.Max);

					// Capped at 30 so the result is right whether the operand is 32- or 64-bit: C# masks
					// the shift count by the operand's width, which is not known here.
					case SyntaxKind.LeftShiftExpression when l.Min >= 0 && Constant(r) is { } shift && shift <= 30:
						return new ValueRange(l.Min * (1L << (int) shift), l.Max * (1L << (int) shift));

					case SyntaxKind.RightShiftExpression when l.Min >= 0 && Constant(r) is { } offset && offset <= 30:
						return new ValueRange(l.Min >> (int) offset, l.Max >> (int) offset);

					default:
						return null;
				}
			}
		}
		catch (OverflowException)
		{
			return null;
		}
	}

	/// <summary>
	///   A cast only preserves the value when the whole range already fits the target type; otherwise
	///   it truncates, which is not monotonic and so tells us nothing.
	/// </summary>
	private static ValueRange? WithoutTruncation(TypeSyntax type, ValueRange? range)
	{
		if (range is not { } value || type is not PredefinedTypeSyntax predefined)
		{
			return null;
		}

		var target = predefined.Keyword.Kind() switch
		{
			SyntaxKind.SByteKeyword => new ValueRange(SByte.MinValue, SByte.MaxValue),
			SyntaxKind.ByteKeyword => new ValueRange(Byte.MinValue, Byte.MaxValue),
			SyntaxKind.ShortKeyword => new ValueRange(Int16.MinValue, Int16.MaxValue),
			SyntaxKind.UShortKeyword => new ValueRange(UInt16.MinValue, UInt16.MaxValue),
			SyntaxKind.CharKeyword => new ValueRange(Char.MinValue, Char.MaxValue),
			SyntaxKind.IntKeyword => new ValueRange(Int32.MinValue, Int32.MaxValue),
			SyntaxKind.UIntKeyword => new ValueRange(UInt32.MinValue, UInt32.MaxValue),
			SyntaxKind.LongKeyword => new ValueRange(Int64.MinValue, Int64.MaxValue),
			_ => (ValueRange?) null
		};

		return target is { } bounds && value.Min >= bounds.Min && value.Max <= bounds.Max ? value : null;
	}

	/// <summary>
	///   Refuses a computed interval that reaches below zero. Unsigned arithmetic wraps rather than
	///   going negative and the operand types are not available here, so such a range may not describe
	///   the value at all. See the class remarks.
	/// </summary>
	private static ValueRange? NonNegative(ValueRange? range)
	{
		return range is { Min: >= 0 } ? range : null;
	}

	private static ValueRange? Narrow(ValueRange? current, ValueRange? addition)
	{
		if (current is not { } value)
		{
			return addition;
		}

		return addition is { } extra ? value.Intersect(extra) : value;
	}

	/// <summary>The value a range pins down, when it pins down a single non-negative one.</summary>
	private static long? Constant(ValueRange? range)
	{
		return range is { IsSinglePoint: true, Min: >= 0 } value ? value.Min : null;
	}

	private static bool IsNamed(ExpressionSyntax expression, string name)
	{
		return expression is IdentifierNameSyntax identifier && identifier.Identifier.Text == name;
	}

	private static bool Writes(SyntaxNode scope, string name)
	{
		var written = new HashSet<string>();
		CollectWritten(scope, written);

		return written.Contains(name);
	}

	/// <summary>
	///   Every name <paramref name="scope" /> can assign to. Deliberately not
	///   <see cref="LoopInvariance.CollectWrittenInLoop" />: that one does not count a <c>ref</c>/
	///   <c>out</c> argument, and widening it would change what LICM and loop unswitching hoist.
	/// </summary>
	private static void CollectWritten(SyntaxNode scope, HashSet<string> written)
	{
		foreach (var node in scope.DescendantNodesAndSelf())
		{
			switch (node)
			{
				case AssignmentExpressionSyntax { Left: IdentifierNameSyntax assigned }:
					written.Add(assigned.Identifier.Text);
					break;

				case PostfixUnaryExpressionSyntax { Operand: IdentifierNameSyntax stepped } postfix
					when postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression):
					written.Add(stepped.Identifier.Text);
					break;

				case PrefixUnaryExpressionSyntax { Operand: IdentifierNameSyntax advanced } prefix
					when prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression):
					written.Add(advanced.Identifier.Text);
					break;

				case ArgumentSyntax { RefKindKeyword.RawKind: not 0, Expression: IdentifierNameSyntax passed }:
					written.Add(passed.Identifier.Text);
					break;
			}
		}
	}

	/// <summary>
	///   The literal's value as a <c>long</c>. A <c>ulong</c> past <see cref="long.MaxValue" /> has no
	///   faithful representation here and is refused rather than wrapped; so is anything non-integral.
	/// </summary>
	private static long? AsLong(object? value)
	{
		return value switch
		{
			int number => number,
			long number => number,
			short number => number,
			sbyte number => number,
			byte number => number,
			ushort number => number,
			uint number => number,
			char character => character,
			ulong number when number <= Int64.MaxValue => (long) number,
			_ => null
		};
	}
}
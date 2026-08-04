using System;
using System.Collections.Generic;
using ConstExpr.SourceGenerator.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Converts a <c>while</c> loop into a <c>do</c>-<c>while</c> loop when the condition is proven
///   true on the very first check, dropping the now-redundant initial test:
///   <code>
///   var i = 1;                         var i = 1;
///   while (i &lt;= n) { ... i++; }   =>   do { ... i++; } while (i &lt;= n);
///   </code>
///   <para>
///     <see cref="ValueRangeAnalysis" /> cannot answer this directly: its outward walk kills any fact
///     about a name the moment it passes through an ancestor loop that writes that name (see
///     <c>ValueRangeAnalysis.FromFacts</c>), because ordinarily such a fact only held on entry, not on
///     the iteration the use belongs to. Entry is exactly what this pass asks about, and the condition
///     sits inside the very loop that writes its own counter, so it is caught by that same kill.
///     Resolving it instead means finding the counter's <em>preceding</em> declaration or assignment in
///     the loop's own enclosing block and handing that expression — which is not inside the loop at all
///     — to the ordinary, still-safe <see cref="ValueRangeAnalysis.For" />. An operand the loop does not
///     write (a bound guarded earlier in the method) goes through <see cref="ValueRangeAnalysis.For" />
///     unchanged, guard-narrowing included.
///   </para>
///   <para>
///     Only the loop keyword changes: the condition expression is left exactly as written and is still
///     evaluated on every iteration by the <c>do</c>-<c>while</c>'s trailer, so this never risks turning
///     a terminating loop into an infinite one the way folding the condition itself would.
///   </para>
/// </summary>
public sealed class WhileToDoWhileRewriter : CSharpSyntaxRewriter
{
	private readonly SyntaxNode _root;

	private WhileToDoWhileRewriter(SyntaxNode root)
	{
		_root = root;
	}

	/// <summary>
	///   Applies while-to-do-while conversion to the supplied syntax node.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node)
	{
		return new WhileToDoWhileRewriter(node).Visit(node);
	}

	public override SyntaxNode VisitWhileStatement(WhileStatementSyntax node)
	{
		var visited = (WhileStatementSyntax) base.VisitWhileStatement(node)!;

		return TryConvert(node, visited) is { } converted ? converted : visited;
	}

	/// <summary>
	///   <paramref name="original" /> is analysed against — its subtree is still attached to
	///   <see cref="_root" />, which <see cref="ValueRangeAnalysis.For" />'s ancestor walk needs to reach
	///   the guards outside the loop. <paramref name="visited" /> supplies the body and condition the
	///   replacement is built from, so any rewriting nested loops already underwent still carries through.
	/// </summary>
	private DoStatementSyntax? TryConvert(WhileStatementSyntax original, WhileStatementSyntax visited)
	{
		// A sibling statement declaring the counter is how the loop-written case below resolves an
		// entry value, so there must be a block to look at.
		if (original.Parent is not BlockSyntax block)
		{
			return null;
		}

		// Also excludes while(true)/while(false)/while(flag) — none are a comparison, so none can be
		// settled by interval analysis. TailRecursionRewriter emits exactly a while(true), and it must
		// keep not firing here.
		if (original.Condition is not BinaryExpressionSyntax comparison || !ValueRangeAnalysis.IsComparison(comparison.Kind()))
		{
			return null;
		}

		if (!LoopInvariance.IsPureExpression(comparison))
		{
			return null;
		}

		var written = LoopInvariance.CollectWrittenInLoop(original);

		if (RangeAtEntry(comparison.Left, written, block, original) is not { } left
		    || RangeAtEntry(comparison.Right, written, block, original) is not { } right)
		{
			return null;
		}

		return ValueRangeAnalysis.Decide(comparison.Kind(), left, right) == true
			? DoStatement(visited.Statement, visited.Condition).WithTriviaFrom(visited)
			: null;
	}

	/// <summary>
	///   The interval <paramref name="operand" /> is known to fall in immediately before
	///   <paramref name="loop" /> runs. A name the loop writes cannot go through
	///   <see cref="ValueRangeAnalysis.For" /> (see the class remarks), so its entry value is resolved
	///   from a plain preceding declaration or assignment in <paramref name="block" /> instead, falling
	///   back to whatever an enclosing loop's own condition already proved about it; a name the loop
	///   leaves alone is safe to hand straight to the general analysis.
	/// </summary>
	private ValueRange? RangeAtEntry(ExpressionSyntax operand, HashSet<string> written, BlockSyntax block, WhileStatementSyntax loop)
	{
		if (operand is not IdentifierNameSyntax identifier || !written.Contains(identifier.Identifier.Text))
		{
			return ValueRangeAnalysis.For(operand, _root);
		}

		var name = identifier.Identifier.Text;

		return RangeOfPrecedingDefinition(block, loop, name) ?? RangeFromEnclosingLoopCondition(block, loop, name);
	}

	/// <summary>
	///   The entry-time range of <paramref name="name" />, derived from the most recent expression
	///   assigned to it — via either a local declaration's initializer or a plain top-level
	///   <c>name = expr;</c> — among the statements of <paramref name="block" /> that run before
	///   <paramref name="loop" />. A statement that writes <paramref name="name" /> some other way
	///   invalidates whatever was found so far: it may have replaced the value that definition described.
	///   <para>
	///     A call to the generated <c>FastAbs</c> helper is trusted non-negative — <c>[1, MaxValue]</c> —
	///     only when its argument has a dominating <c>if (arg == 0) return/throw;</c> guard earlier in
	///     the same block: <c>FastAbs&lt;T&gt;</c> is <c>T.IsNegative(x) ? -x : x</c>, which <em>wraps</em>
	///     rather than throws at <c>T.MinValue</c> (so it matches the ternary idiom it replaces there),
	///     so a bare <c>FastAbs(x)</c> alone proves nothing about the sign of its result. <c>T.MinValue</c>
	///     is deliberately treated as out of contract for this recognition, same as the idiom it replaces.
	///     Every other definition shape goes through the ordinary <see cref="ValueRangeAnalysis.For" />.
	///   </para>
	/// </summary>
	private ValueRange? RangeOfPrecedingDefinition(BlockSyntax block, WhileStatementSyntax loop, string name)
	{
		ExpressionSyntax? definition = null;
		var definedFromNonZeroAbs = false;
		var provenNonZero = new HashSet<string>();

		foreach (var statement in block.Statements)
		{
			if (statement == loop)
			{
				break;
			}

			(definition, definedFromNonZeroAbs) = Advance(statement, name, definition, definedFromNonZeroAbs, provenNonZero);
		}

		if (definedFromNonZeroAbs)
		{
			return new ValueRange(1, Int64.MaxValue);
		}

		return definition is { } expr ? ValueRangeAnalysis.For(expr, _root) : null;
	}

	/// <summary>
	///   The entry-time range of <paramref name="name" /> when <paramref name="block" /> has no
	///   preceding definition of its own, but <paramref name="block" /> is itself the body of an
	///   enclosing <c>while</c> whose own condition already constrains the same name. That condition
	///   held to enter the body at all, and — provided nothing between the top of the body and
	///   <paramref name="loop" /> touches <paramref name="name" /> — it still describes the value right
	///   here, on every iteration of the outer loop, not just the first.
	///   <para>
	///     Deliberately matches only <see cref="WhileStatementSyntax" />, never a <c>do</c>-<c>while</c>:
	///     a <c>do</c>-<c>while</c>'s body runs once before its condition is ever checked, so entering it
	///     proves nothing about what that condition tests.
	///   </para>
	/// </summary>
	private ValueRange? RangeFromEnclosingLoopCondition(BlockSyntax block, WhileStatementSyntax loop, string name)
	{
		foreach (var statement in block.Statements)
		{
			if (statement == loop)
			{
				break;
			}

			if (Declares(statement, name) || LoopInvariance.CollectWrittenInLoop(statement).Contains(name))
			{
				return null;
			}
		}

		return block.Parent is WhileStatementSyntax outer && outer.Statement == block
			? RangeFromComparison(outer.Condition, name)
			: null;
	}

	/// <summary>Whether <paramref name="statement" /> declares a local variable named <paramref name="name" />.</summary>
	private static bool Declares(StatementSyntax statement, string name)
	{
		if (statement is not LocalDeclarationStatementSyntax { Declaration.Variables: { } variables })
		{
			return false;
		}

		foreach (var variable in variables)
		{
			if (variable.Identifier.Text == name)
			{
				return true;
			}
		}

		return false;
	}

	/// <summary>
	///   The range a comparison of shape <c>name &lt;op&gt; bound</c> (either operand order) proves for
	///   <paramref name="name" /> when it holds, or <see langword="null" /> for any other shape. The bound
	///   itself goes through the ordinary <see cref="ValueRangeAnalysis.For" />, so a non-literal bound
	///   still resolves whenever the general analysis can already narrow it.
	/// </summary>
	private ValueRange? RangeFromComparison(ExpressionSyntax condition, string name)
	{
		if (condition is not BinaryExpressionSyntax comparison || !ValueRangeAnalysis.IsComparison(comparison.Kind()))
		{
			return null;
		}

		var kind = comparison.Kind();
		ExpressionSyntax boundExpression;

		if (comparison.Left is IdentifierNameSyntax left && left.Identifier.Text == name)
		{
			boundExpression = comparison.Right;
		}
		else if (comparison.Right is IdentifierNameSyntax right && right.Identifier.Text == name)
		{
			boundExpression = comparison.Left;
			kind = Mirror(kind);
		}
		else
		{
			return null;
		}

		if (ValueRangeAnalysis.For(boundExpression, _root) is not { } bound)
		{
			return null;
		}

		return kind switch
		{
			SyntaxKind.GreaterThanExpression => new ValueRange(bound.Min + 1, Int64.MaxValue),
			SyntaxKind.GreaterThanOrEqualExpression => new ValueRange(bound.Min, Int64.MaxValue),
			SyntaxKind.LessThanExpression => new ValueRange(Int64.MinValue, bound.Max - 1),
			SyntaxKind.LessThanOrEqualExpression => new ValueRange(Int64.MinValue, bound.Max),
			_ => null
		};
	}

	/// <summary>Flips a comparison's operator to match its operands being swapped.</summary>
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

	/// <summary>
	///   Folds <paramref name="statement" /> into the running <paramref name="definition" />/
	///   <paramref name="definedFromNonZeroAbs" /> state <see cref="RangeOfPrecedingDefinition" /> tracks,
	///   and records any zero-guard it establishes in <paramref name="provenNonZero" />. Order matters:
	///   the guard set still reflects every guard strictly before this statement when
	///   <see cref="IsNonZeroAbsCall" /> is checked, before this statement's own write (if any) retires
	///   the fact it may itself depend on — which matters when it reassigns the very name a guard proved
	///   nonzero (<c>n = FastAbs(n);</c> writes <c>n</c> in the same statement whose argument the guard
	///   described).
	/// </summary>
	private static (ExpressionSyntax? definition, bool definedFromNonZeroAbs) Advance(
		StatementSyntax statement, string name, ExpressionSyntax? definition, bool definedFromNonZeroAbs, HashSet<string> provenNonZero)
	{
		if (statement is IfStatementSyntax { Else: null } guard && Exits(guard.Statement) && ZeroCheckedName(guard.Condition) is { } guardedName)
		{
			provenNonZero.Add(guardedName);
		}

		var found = statement switch
		{
			LocalDeclarationStatementSyntax { Declaration.Variables: [ { Initializer.Value: { } value } declarator ] } when declarator.Identifier.Text == name => value,
			ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { RawKind: (int) SyntaxKind.SimpleAssignmentExpression, Left: IdentifierNameSyntax target, Right: { } rhs } } when target.Identifier.Text == name => rhs,
			_ => null
		};

		if (found is not null)
		{
			definition = found;
			definedFromNonZeroAbs = IsNonZeroAbsCall(found, provenNonZero);
		}
		else if (LoopInvariance.CollectWrittenInLoop(statement).Contains(name))
		{
			definition = null;
			definedFromNonZeroAbs = false;
		}

		provenNonZero.ExceptWith(LoopInvariance.CollectWrittenInLoop(statement));

		return (definition, definedFromNonZeroAbs);
	}

	/// <summary>
	///   Whether <paramref name="expression" /> is a call to the generated <c>FastAbs</c> helper whose
	///   argument is a name <paramref name="provenNonZero" /> already covers.
	/// </summary>
	private static bool IsNonZeroAbsCall(ExpressionSyntax expression, HashSet<string> provenNonZero)
	{
		return expression is InvocationExpressionSyntax
		{
			Expression: IdentifierNameSyntax { Identifier.Text: "FastAbs" },
			ArgumentList.Arguments: [ { Expression: IdentifierNameSyntax argument } ]
		} && provenNonZero.Contains(argument.Identifier.Text);
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

	/// <summary>The identifier a bare <c>name == 0</c> condition checks, or <see langword="null" /> for any other shape.</summary>
	private static string? ZeroCheckedName(ExpressionSyntax condition)
	{
		if (condition is not BinaryExpressionSyntax { RawKind: (int) SyntaxKind.EqualsExpression } binary)
		{
			return null;
		}

		if (binary.Left is IdentifierNameSyntax left && binary.Right.IsNumericZero())
		{
			return left.Identifier.Text;
		}

		if (binary.Right is IdentifierNameSyntax right && binary.Left.IsNumericZero())
		{
			return right.Identifier.Text;
		}

		return null;
	}
}
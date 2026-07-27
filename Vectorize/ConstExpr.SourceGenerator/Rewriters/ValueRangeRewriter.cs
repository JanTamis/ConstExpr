using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Performs Value-Range Propagation: a comparison whose outcome is already settled by the intervals
///   <see cref="ValueRangeAnalysis" /> derives for its operands is replaced by <c>true</c> or
///   <c>false</c>, and the branch that can no longer be taken is dropped:
///   <code>
///   for (var i = 0; i &lt; 8; i++)          for (var i = 0; i &lt; 8; i++)
///       if (i &lt; 10) sum += data[i];   =>       sum += data[i];
///   </code>
///   <para>
///     The collapse is this pass's own job. <see cref="DeadCodePruner" /> only drops an <c>if</c>
///     whose body came out empty — it never looks at the condition — and nothing else in the pipeline
///     removes an <c>if (true)</c>. Leaving one behind would emit code worse than the input, and the
///     Sample build would not notice because it still compiles.
///   </para>
///   <para>
///     A surviving block is spliced into its parent when it declares no names of its own. That keeps
///     the statements in the block CSE actually scans, which is the point of doing this before CSE
///     runs. It is the opposite of the hazard in collapsing a statement into a ternary branch: code
///     moves from conditional to unconditional, so the eliminator sees more to hoist, not less.
///   </para>
/// </summary>
public sealed class ValueRangeRewriter : CSharpSyntaxRewriter
{
	private readonly SyntaxNode _root;

	private ValueRangeRewriter(SyntaxNode root)
	{
		_root = root;
	}

	/// <summary>
	///   Applies value-range propagation to the supplied syntax node.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node)
	{
		return new ValueRangeRewriter(node).Visit(node);
	}

	public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
	{
		var visited = (BinaryExpressionSyntax) base.VisitBinaryExpression(node)!;

		// The conditional-and/or strategies all ran inside the partial rewriter, so a literal this pass
		// just produced has nothing left downstream to absorb it — `(uint) n <= 9U && true` would reach
		// the output verbatim.
		if (node.Kind() is SyntaxKind.LogicalAndExpression or SyntaxKind.LogicalOrExpression)
		{
			return Shortcut(visited) ?? visited;
		}

		if (!ValueRangeAnalysis.IsComparison(node.Kind()) || IsLoopCondition(node) || !LoopInvariance.IsPureExpression(node))
		{
			return visited;
		}

		// Analysed against the original node, which is still attached to the tree the facts live in;
		// `visited` has been detached by its own children being rewritten.
		if (ValueRangeAnalysis.For(node.Left, _root) is not { } left
		    || ValueRangeAnalysis.For(node.Right, _root) is not { } right
		    || ValueRangeAnalysis.Decide(node.Kind(), left, right) is not { } outcome)
		{
			return visited;
		}

		return BooleanLiteral(outcome).WithTriviaFrom(visited);
	}

	private static LiteralExpressionSyntax BooleanLiteral(bool value)
	{
		return LiteralExpression(value ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression);
	}

	/// <summary>
	///   The same fold as <see cref="VisitBinaryExpression" />, for the form a comparison chain has
	///   already been canonicalised into by the time this pass runs: <c>x &lt;= 15 &amp;&amp; x &lt; 8</c>
	///   reaches here as <c>x is &lt;= 15 and &lt; 8</c>. A pattern that is settled as a whole becomes a
	///   literal; one that is not still loses the conjuncts that cannot fail.
	/// </summary>
	public override SyntaxNode VisitIsPatternExpression(IsPatternExpressionSyntax node)
	{
		var visited = (IsPatternExpressionSyntax) base.VisitIsPatternExpression(node)!;

		if (!LoopInvariance.IsPureExpression(node.Expression) || ValueRangeAnalysis.For(node.Expression, _root) is not { } subject)
		{
			return visited;
		}

		if (ValueRangeAnalysis.Decide(node.Pattern, subject, _root) is { } outcome)
		{
			return BooleanLiteral(outcome).WithTriviaFrom(visited);
		}

		var reduced = ValueRangeAnalysis.Reduce(node.Pattern, subject, _root);

		if (reduced == node.Pattern)
		{
			return visited;
		}

		// A combinator that lost every conjunct but one is no longer a combinator: `x is <= 15 and < 8`
		// reduces to the bare relational pattern `< 8`, and a bare relational pattern reads better as
		// the binary expression it means (`x < 8`) than left wrapped in `is`.
		if (reduced is RelationalPatternSyntax { OperatorToken: var operatorToken } relational
		    && RelationalBinaryKind(operatorToken.Kind()) is { } binaryKind)
		{
			return BinaryExpression(binaryKind, visited.Expression, relational.Expression).WithTriviaFrom(visited);
		}

		return visited.WithPattern(reduced);
	}

	private static SyntaxKind? RelationalBinaryKind(SyntaxKind operatorTokenKind)
	{
		return operatorTokenKind switch
		{
			SyntaxKind.LessThanToken => SyntaxKind.LessThanExpression,
			SyntaxKind.LessThanEqualsToken => SyntaxKind.LessThanOrEqualExpression,
			SyntaxKind.GreaterThanToken => SyntaxKind.GreaterThanExpression,
			SyntaxKind.GreaterThanEqualsToken => SyntaxKind.GreaterThanOrEqualExpression,
			_ => null
		};
	}

	public override SyntaxNode VisitBlock(BlockSyntax node)
	{
		var visited = (BlockSyntax) base.VisitBlock(node)!;
		var statements = new List<StatementSyntax>();
		var collapsed = false;

		foreach (var statement in visited.Statements)
		{
			if (statement is not IfStatementSyntax branch || Outcome(branch.Condition) is not { } taken)
			{
				statements.Add(statement);
				continue;
			}

			collapsed = true;

			switch (taken ? branch.Statement : branch.Else?.Statement)
			{
				// Splicing is only safe when the block introduces nothing: a name declared inside it
				// would suddenly share a scope with the rest of this block.
				case BlockSyntax inner when !Declares(inner):
					statements.AddRange(inner.Statements);
					break;

				case { } survivor:
					statements.Add(survivor.WithTriviaFrom(branch));
					break;
			}
		}

		return collapsed ? visited.WithStatements(List(statements)) : visited;
	}

	public override SyntaxNode VisitIfStatement(IfStatementSyntax node)
	{
		var visited = (IfStatementSyntax) base.VisitIfStatement(node)!;

		// A statement inside a block is handled by VisitBlock, which can splice rather than nest.
		if (node.Parent is BlockSyntax || Outcome(visited.Condition) is not { } taken)
		{
			return visited;
		}

		// An empty block rather than nothing at all: `while (x) if (false) y;` has a statement
		// position that must stay filled.
		return (taken ? visited.Statement : visited.Else?.Statement) ?? Block();
	}

	public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
	{
		var visited = (WhileStatementSyntax) base.VisitWhileStatement(node)!;

		return Outcome(visited.Condition) is false ? null : visited;
	}

	public override SyntaxNode VisitConditionalExpression(ConditionalExpressionSyntax node)
	{
		var visited = (ConditionalExpressionSyntax) base.VisitConditionalExpression(node)!;

		return Outcome(visited.Condition) is { } taken
			? (taken ? visited.WhenTrue : visited.WhenFalse).WithTriviaFrom(visited)
			: visited;
	}

	/// <summary>
	///   Absorbs a boolean literal one side of an <c>&amp;&amp;</c> or <c>||</c> has just been folded
	///   into. Every case here drops only an operand that is itself a literal, or one short-circuiting
	///   meant was never evaluated — so nothing observable is lost.
	///   <para>
	///     Deliberately missing is the fourth case, <c>a &amp;&amp; false</c> collapsing to
	///     <c>false</c>. That one discards <c>a</c>, which really would have run, and no guard
	///     available here can tell whether that matters:
	///     <see cref="LoopInvariance.IsPureExpression" /> admits invocations and element accesses
	///     despite its name, so it would happily drop a <c>Foo()</c> or an <c>arr[i]</c> that throws.
	///     The lost cleanup is worth less than the guarantee that this pass only removes dead code.
	///   </para>
	/// </summary>
	private static ExpressionSyntax? Shortcut(BinaryExpressionSyntax node)
	{
		var conjunction = node.IsKind(SyntaxKind.LogicalAndExpression);
		var left = Outcome(node.Left);

		// A false `&&` (or true `||`) on the left settles the result, and short-circuiting means the
		// right side was never going to run.
		if (left == !conjunction)
		{
			return node.Left.WithTriviaFrom(node);
		}

		// A neutral literal on either side leaves the other operand standing. This is the case that
		// clears up after the comparison fold: `(uint) n <= 9U && true`.
		if (left == conjunction)
		{
			return node.Right.WithTriviaFrom(node);
		}

		return Outcome(node.Right) == conjunction ? node.Left.WithTriviaFrom(node) : null;
	}

	/// <summary>
	///   Whether a condition has already been reduced to a bare boolean literal by this pass. Only the
	///   literal itself — the boolean algebra around it belongs to the conditional-operator strategies.
	/// </summary>
	private static bool? Outcome(ExpressionSyntax condition)
	{
		return condition switch
		{
			LiteralExpressionSyntax { RawKind: (int) SyntaxKind.TrueLiteralExpression } => true,
			LiteralExpressionSyntax { RawKind: (int) SyntaxKind.FalseLiteralExpression } => false,
			_ => null
		};
	}

	/// <summary>
	///   Folding the condition of a loop is how a terminating loop becomes an infinite one. The
	///   analysis already refuses a fact about a name the loop body writes, but the top-level condition
	///   is refused outright — it is the one place where being wrong does not merely emit worse code.
	/// </summary>
	private static bool IsLoopCondition(BinaryExpressionSyntax node)
	{
		return node.Parent switch
		{
			ForStatementSyntax loop => loop.Condition == node,
			WhileStatementSyntax loop => loop.Condition == node,
			DoStatementSyntax loop => loop.Condition == node,
			_ => false
		};
	}

	private static bool Declares(BlockSyntax block)
	{
		return block.Statements.Any(statement => statement is LocalDeclarationStatementSyntax or LocalFunctionStatementSyntax)
		       || block.DescendantNodes().OfType<SingleVariableDesignationSyntax>().Any();
	}
}
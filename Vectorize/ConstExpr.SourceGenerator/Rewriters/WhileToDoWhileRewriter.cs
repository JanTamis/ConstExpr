using System.Collections.Generic;
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
	///   from a plain preceding declaration or assignment in <paramref name="block" /> instead; a name
	///   the loop leaves alone is safe to hand straight to the general analysis.
	/// </summary>
	private ValueRange? RangeAtEntry(ExpressionSyntax operand, HashSet<string> written, BlockSyntax block, WhileStatementSyntax loop)
	{
		if (operand is IdentifierNameSyntax identifier && written.Contains(identifier.Identifier.Text))
		{
			return PrecedingDefinition(block, loop, identifier.Identifier.Text) is { } definition
				? ValueRangeAnalysis.For(definition, _root)
				: null;
		}

		return ValueRangeAnalysis.For(operand, _root);
	}

	/// <summary>
	///   The most recent expression assigned to <paramref name="name" /> among the statements of
	///   <paramref name="block" /> that run before <paramref name="before" /> — via either a local
	///   declaration's initializer or a plain top-level <c>name = expr;</c>. A statement that writes
	///   <paramref name="name" /> some other way invalidates whatever was found so far: it may have
	///   replaced the value that definition described.
	/// </summary>
	private static ExpressionSyntax? PrecedingDefinition(BlockSyntax block, StatementSyntax before, string name)
	{
		ExpressionSyntax? definition = null;

		foreach (var statement in block.Statements)
		{
			if (statement == before)
			{
				break;
			}

			definition = statement switch
			{
				LocalDeclarationStatementSyntax { Declaration.Variables: [ { Initializer.Value: { } value } declarator ] } when declarator.Identifier.Text == name => value,
				ExpressionStatementSyntax { Expression: AssignmentExpressionSyntax { RawKind: (int) SyntaxKind.SimpleAssignmentExpression, Left: IdentifierNameSyntax target, Right: { } rhs } } when target.Identifier.Text == name => rhs,
				_ when LoopInvariance.CollectWrittenInLoop(statement).Contains(name) => null,
				_ => definition
			};
		}

		return definition;
	}
}
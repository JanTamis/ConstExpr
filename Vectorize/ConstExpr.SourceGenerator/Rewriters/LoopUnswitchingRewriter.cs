using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Performs Loop Unswitching: when a loop body consists solely of an <c>if</c> whose condition
///   is loop-invariant (references nothing written or declared inside the loop and is
///   side-effect-free), the branch is hoisted out and the loop is duplicated per arm:
///   <code>
///   LOOP { if (cond) { A } else { B } }
///   =>
///   if (cond) { LOOP { A } } else { LOOP { B } }
///   </code>
///   The condition is then tested once instead of on every iteration. Because <c>cond</c> is
///   invariant, its value is identical on every iteration, so hoisting the test cannot change
///   which arm runs. Applies to <c>for</c>, <c>while</c>, <c>do-while</c> and <c>foreach</c> loops.
///   Scope is deliberately narrow (v1): the loop body must be exactly one <c>if</c> statement (no
///   surrounding statements, no <c>else if</c> chain). This is the clean case that needs no
///   reasoning about duplicated surrounding side effects; broader shapes are left unchanged.
/// </summary>
public sealed class LoopUnswitchingRewriter : CSharpSyntaxRewriter
{
	/// <summary>
	///   Applies loop unswitching to the supplied syntax node.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node)
	{
		return new LoopUnswitchingRewriter().Visit(node);
	}

	// Each loop visit recurses first (bottom-up, so nested loops unswitch before their enclosing
	// loop), then attempts to replace the loop in-place with the hoisted `if`.

	public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
	{
		var visited = (ForStatementSyntax) base.VisitForStatement(node)!;
		return TryUnswitch(visited, visited.Statement) ?? visited;
	}

	public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
	{
		var visited = (WhileStatementSyntax) base.VisitWhileStatement(node)!;
		return TryUnswitch(visited, visited.Statement) ?? visited;
	}

	public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
	{
		var visited = (DoStatementSyntax) base.VisitDoStatement(node)!;
		return TryUnswitch(visited, visited.Statement) ?? visited;
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		var visited = (ForEachStatementSyntax) base.VisitForEachStatement(node)!;
		return TryUnswitch(visited, visited.Statement) ?? visited;
	}

	/// <summary>
	///   If <paramref name="loopBody" /> is a block whose only statement is an <c>if</c> with a
	///   loop-invariant condition, returns the unswitched <c>if</c> (loop duplicated per arm);
	///   otherwise <see langword="null" />.
	/// </summary>
	private static StatementSyntax? TryUnswitch(StatementSyntax loop, StatementSyntax loopBody)
	{
		if (loopBody is not BlockSyntax { Statements: [ IfStatementSyntax ifStmt ] } bodyBlock)
		{
			return null;
		}

		// `else if` (an if nested directly in the else) is out of scope — one condition only.
		if (ifStmt.Else?.Statement is IfStatementSyntax)
		{
			return null;
		}

		var condition = ifStmt.Condition;

		// Scan the whole loop (not just the body): a `for` incrementor or a side-effecting loop
		// condition can also mutate variables the `if` condition reads, and those must count as
		// non-invariant. CollectLoopLocals over the whole loop also captures a `for`'s own iteration
		// variable via its initializer declarator.
		var written = LoopInvariance.CollectWrittenInLoop(loop);
		var locals = LoopInvariance.CollectLoopLocals(loop);

		// A `foreach` variable is a plain identifier token, not a declarator/designation, so it is
		// not picked up by CollectLoopLocals — add it explicitly (it changes every iteration).
		if (loop is ForEachStatementSyntax foreachStmt)
		{
			written.Add(foreachStmt.Identifier.Text);
		}

		if (!IsInvariant(condition, written, locals))
		{
			return null;
		}

		// Rebuild the loop once per arm by swapping its body block for the arm's body.
		// No else → the false arm keeps an empty-bodied loop, preserving the loop's own
		// iteration side effects (condition / incrementor) on that path.
		var thenLoop = loop.ReplaceNode(bodyBlock, AsBlock(ifStmt.Statement));
		var elseLoop = loop.ReplaceNode(bodyBlock, ifStmt.Else is { } elseClause ? AsBlock(elseClause.Statement) : Block());

		return IfStatement(condition, Block(thenLoop))
			.WithElse(ElseClause(Block(elseLoop)))
			.WithTriviaFrom(loop);
	}

	private static bool IsInvariant(ExpressionSyntax condition, HashSet<string> written, HashSet<string> locals)
	{
		if (!LoopInvariance.IsPureExpression(condition))
		{
			return false;
		}

		return !condition
			.DescendantNodesAndSelf()
			.OfType<IdentifierNameSyntax>()
			.Any(id => written.Contains(id.Identifier.Text) || locals.Contains(id.Identifier.Text));
	}

	private static BlockSyntax AsBlock(StatementSyntax statement)
	{
		return statement as BlockSyntax ?? Block(statement);
	}
}
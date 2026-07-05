using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Performs Loop Fusion: two directly adjacent loops with identical iteration spaces are merged
///   into one loop, so the loop overhead (counter, bound check) is paid once and the combined body
///   is exposed to the other passes as a single unit:
///   <code>
///   for (var i = 0; i &lt; n; i++) { A; }
///   for (var i = 0; i &lt; n; i++) { B; }
///   =>
///   for (var i = 0; i &lt; n; i++) { A; B; }
///   </code>
///   Fusion reorders work across iterations (all of A no longer runs before any of B), so it is
///   only applied when that reordering is provably unobservable. Scope is deliberately narrow (v1):
///   <list type="bullet">
///     <item>
///       <description>
///         <c>for</c>+<c>for</c> with structurally identical declaration/condition/
///         incrementor (same loop-variable name) and a monotonic <c>i++</c>/<c>i--</c>/<c>i += c</c>
///         style incrementor, or <c>foreach</c>+<c>foreach</c> over the same pure collection expression
///         with the same iteration variable.
///       </description>
///     </item>
///     <item>
///       <description>
///         Neither body may contain control-flow escapes (<c>break</c>,
///         <c>continue</c>, <c>goto</c>, <c>return</c>, <c>yield</c>, <c>throw</c>, <c>await</c>),
///         calls, object creations, lambdas or local functions — any of those could make the
///         interleaving observable.
///       </description>
///     </item>
///     <item>
///       <description>
///         No data dependence between the bodies: a variable written in one body and
///         touched in the other blocks fusion, except an array accessed in both bodies exclusively as
///         <c>x[i]</c> with the shared (monotonic, hence non-repeating) loop counter — a distance-0
///         dependence, which fusion preserves.
///       </description>
///     </item>
///   </list>
///   Anything outside these shapes is left unchanged. Applies bottom-up, repeatedly, so three
///   adjacent fusable loops collapse into one.
/// </summary>
public sealed class LoopFusionRewriter : CSharpSyntaxRewriter
{
	/// <summary>
	///   Applies loop fusion to the supplied syntax node.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node)
	{
		return new LoopFusionRewriter().Visit(node);
	}

	public override SyntaxNode? VisitBlock(BlockSyntax node)
	{
		// Children first (bottom-up), so loops nested in if/for bodies fuse before this block's.
		var visited = (BlockSyntax) base.VisitBlock(node)!;
		var statements = new List<StatementSyntax>(visited.Statements);
		var changed = false;

		// Pairwise fusion without advancing on success, so a fused loop can fuse with the next one.
		for (var i = 0; i < statements.Count - 1;)
		{
			if (TryFuse(statements[i], statements[i + 1]) is { } fused)
			{
				statements[i] = fused;
				statements.RemoveAt(i + 1);
				changed = true;
			}
			else
			{
				i++;
			}
		}

		return changed ? visited.WithStatements(List(statements)) : visited;
	}

	/// <summary>
	///   Returns the fused loop when <paramref name="first" /> and <paramref name="second" /> are
	///   adjacent loops with identical iteration spaces and independent bodies; <see langword="null" />
	///   otherwise.
	/// </summary>
	private static StatementSyntax? TryFuse(StatementSyntax first, StatementSyntax second)
	{
		switch (first, second)
		{
			case (ForStatementSyntax { Statement: BlockSyntax body1 } for1, ForStatementSyntax { Statement: BlockSyntax body2 } for2):
			{
				// v1 shape: single declared counter, a condition, and exactly one incrementor.
				if (for1.Declaration is not { Variables: [ { } declarator1 ] }
				    || for2.Declaration is not { Variables.Count: 1 }
				    || for1.Initializers.Count > 0 || for2.Initializers.Count > 0
				    || for1.Condition is null || for2.Condition is null
				    || for1.Incrementors is not [ { } incrementor ] || for2.Incrementors.Count != 1)
				{
					return null;
				}

				// Identical iteration space, established structurally (text compare, not the
				// hash-based comparer: a hash collision must never fuse loops with different bounds).
				if (!StructurallyEqual(for1.Declaration, for2.Declaration)
				    || !StructurallyEqual(for1.Condition, for2.Condition)
				    || !StructurallyEqual(incrementor, for2.Incrementors[0]))
				{
					return null;
				}

				var loopVar = declarator1.Identifier.Text;

				// A monotonic counter never repeats a value, which is what makes the x[loopVar]
				// dependence exemption below sound.
				if (!IsMonotonicIncrementor(incrementor, loopVar) || !LoopInvariance.IsPureExpression(for1.Condition))
				{
					return null;
				}

				// Identifiers feeding the iteration space must not be written by either body,
				// otherwise the second loop's trip count would have differed from the first's.
				var controlIds = new HashSet<string>(CollectIdentifiers(for1.Declaration)
					.Concat(CollectIdentifiers(for1.Condition))
					.Concat(CollectIdentifiers(incrementor)));
				controlIds.Remove(loopVar);

				if (!BodiesAreIndependent(body1, body2, loopVar, controlIds, true))
				{
					return null;
				}

				return for1.WithStatement(ConcatBodies(body1, body2)).WithTriviaFrom(first);
			}

			case (ForEachStatementSyntax { Statement: BlockSyntax body1 } foreach1, ForEachStatementSyntax { Statement: BlockSyntax body2 } foreach2):
			{
				if (foreach1.Identifier.Text != foreach2.Identifier.Text
				    || !StructurallyEqual(foreach1.Type, foreach2.Type)
				    || !StructurallyEqual(foreach1.Expression, foreach2.Expression)
				    || !LoopInvariance.IsPureExpression(foreach1.Expression))
				{
					return null;
				}

				// No index exemption: a foreach variable can repeat values (e.g. duplicate items),
				// so element accesses keyed on it are not guaranteed distance-0.
				var controlIds = new HashSet<string>(CollectIdentifiers(foreach1.Expression));
				controlIds.Remove(foreach1.Identifier.Text);

				if (!BodiesAreIndependent(body1, body2, foreach1.Identifier.Text, controlIds, false))
				{
					return null;
				}

				return foreach1.WithStatement(ConcatBodies(body1, body2)).WithTriviaFrom(first);
			}

			default:
				return null;
		}
	}

	private static BlockSyntax ConcatBodies(BlockSyntax body1, BlockSyntax body2)
	{
		return body1.WithStatements(body1.Statements.AddRange(body2.Statements));
	}

	/// <summary>
	///   Verifies the two bodies can be interleaved per-iteration without changing behaviour:
	///   no observable constructs, no colliding locals, the loop counter and iteration-space inputs
	///   untouched, and no cross-body data dependence (modulo the <c>x[loopVar]</c> exemption).
	/// </summary>
	private static bool BodiesAreIndependent(BlockSyntax body1, BlockSyntax body2, string loopVar, HashSet<string> controlIds, bool allowIndexExemption)
	{
		if (HasUnfusableConstruct(body1) || HasUnfusableConstruct(body2))
		{
			return false;
		}

		// Locals of the two bodies land in one scope after fusion — same name would redeclare.
		var locals1 = LoopInvariance.CollectLoopLocals(body1);

		if (locals1.Overlaps(LoopInvariance.CollectLoopLocals(body2)))
		{
			return false;
		}

		var writes1 = CollectWrites(body1);
		var writes2 = CollectWrites(body2);

		if (writes1.Contains(loopVar) || writes2.Contains(loopVar))
		{
			return false;
		}

		if (controlIds.Overlaps(writes1) || controlIds.Overlaps(writes2))
		{
			return false;
		}

		// Cross-body dependence: anything written on one side and touched on the other. Reads are
		// over-approximated as "every identifier use", which also covers write/write conflicts.
		var ids1 = new HashSet<string>(CollectIdentifiers(body1));
		var ids2 = new HashSet<string>(CollectIdentifiers(body2));

		var conflicts = new HashSet<string>(writes1.Intersect(ids2).Concat(writes2.Intersect(ids1)));
		conflicts.Remove(loopVar);

		foreach (var name in conflicts)
		{
			// Sole allowed dependence: both sides touch the variable exclusively as x[loopVar].
			// With a monotonic counter each iteration hits a distinct element, so iteration k of
			// body2 only ever depends on iteration k of body1 — exactly what fusion preserves.
			if (!allowIndexExemption
			    || !AccessedOnlyAsElementOfLoopVar(body1, name, loopVar)
			    || !AccessedOnlyAsElementOfLoopVar(body2, name, loopVar))
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	///   Anything that could make the fused interleaving observable, or that redirects control flow
	///   out of the loop body. Nested loops with their own break/continue are blocked too.
	///   <!-- ponytail: blocks all calls/creations; whitelist provably pure ones if this vetoes too many fusions. -->
	/// </summary>
	private static bool HasUnfusableConstruct(BlockSyntax body)
	{
		return body.DescendantNodesAndSelf().Any(n => n
			is BreakStatementSyntax
			or ContinueStatementSyntax
			or GotoStatementSyntax
			or ReturnStatementSyntax
			or YieldStatementSyntax
			or ThrowStatementSyntax
			or ThrowExpressionSyntax
			or AwaitExpressionSyntax
			or InvocationExpressionSyntax
			or BaseObjectCreationExpressionSyntax
			or AnonymousFunctionExpressionSyntax
			or LocalFunctionStatementSyntax);
	}

	/// <summary>
	///   Collects the base identifiers of every mutation in the scope: plain assignments, compound
	///   assignments, inc/dec, and writes through indexers/members (<c>a[i] = …</c> counts as a
	///   write to <c>a</c>). Unlike <see cref="LoopInvariance.CollectWrittenInLoop" />, element and
	///   member writes are attributed to their root variable, which the dependence check needs.
	/// </summary>
	private static HashSet<string> CollectWrites(SyntaxNode scope)
	{
		var writes = new HashSet<string>();

		foreach (var node in scope.DescendantNodes())
		{
			switch (node)
			{
				case AssignmentExpressionSyntax assignment:
					AddBaseIdentifier(assignment.Left, writes);
					break;

				case PrefixUnaryExpressionSyntax pre when pre.IsKind(SyntaxKind.PreIncrementExpression) || pre.IsKind(SyntaxKind.PreDecrementExpression):
					AddBaseIdentifier(pre.Operand, writes);
					break;

				case PostfixUnaryExpressionSyntax post when post.IsKind(SyntaxKind.PostIncrementExpression) || post.IsKind(SyntaxKind.PostDecrementExpression):
					AddBaseIdentifier(post.Operand, writes);
					break;

				case ArgumentSyntax arg when arg.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || arg.RefKindKeyword.IsKind(SyntaxKind.OutKeyword):
					AddBaseIdentifier(arg.Expression, writes);
					break;
			}
		}

		return writes;
	}

	private static void AddBaseIdentifier(ExpressionSyntax expr, HashSet<string> writes)
	{
		while (true)
		{
			switch (expr)
			{
				case ParenthesizedExpressionSyntax p:
					expr = p.Expression;
					continue;
				case ElementAccessExpressionSyntax e:
					expr = e.Expression;
					continue;
				case MemberAccessExpressionSyntax m:
					expr = m.Expression;
					continue;
				case IdentifierNameSyntax id:
					writes.Add(id.Identifier.Text);
					return;
				default:
					return;
			}
		}
	}

	private static IEnumerable<string> CollectIdentifiers(SyntaxNode scope)
	{
		return scope.DescendantNodesAndSelf()
			.OfType<IdentifierNameSyntax>()
			.Select(id => id.Identifier.Text);
	}

	/// <summary>
	///   Every occurrence of <paramref name="name" /> in the body must be the target of an element
	///   access indexed by exactly the loop variable (<c>name[loopVar]</c>) — no bare uses, no other
	///   index expressions.
	/// </summary>
	private static bool AccessedOnlyAsElementOfLoopVar(BlockSyntax body, string name, string loopVar)
	{
		foreach (var id in body.DescendantNodes().OfType<IdentifierNameSyntax>())
		{
			if (id.Identifier.Text != name)
			{
				continue;
			}

			if (id.Parent is not ElementAccessExpressionSyntax { ArgumentList.Arguments: [ { Expression: IdentifierNameSyntax index } ] } element
			    || element.Expression != id
			    || index.Identifier.Text != loopVar)
			{
				return false;
			}
		}

		return true;
	}

	/// <summary>
	///   Only counters that move strictly in one direction qualify: <c>i++</c>, <c>++i</c>,
	///   <c>i--</c>, <c>--i</c>, or <c>i += / -= (nonzero numeric literal)</c>. Such a counter never
	///   revisits a value, which the <c>x[loopVar]</c> dependence exemption relies on.
	/// </summary>
	private static bool IsMonotonicIncrementor(ExpressionSyntax incrementor, string loopVar)
	{
		switch (incrementor)
		{
			case PostfixUnaryExpressionSyntax { Operand: IdentifierNameSyntax post } postfix
				when postfix.IsKind(SyntaxKind.PostIncrementExpression) || postfix.IsKind(SyntaxKind.PostDecrementExpression):
				return post.Identifier.Text == loopVar;

			case PrefixUnaryExpressionSyntax { Operand: IdentifierNameSyntax pre } prefix
				when prefix.IsKind(SyntaxKind.PreIncrementExpression) || prefix.IsKind(SyntaxKind.PreDecrementExpression):
				return pre.Identifier.Text == loopVar;

			case AssignmentExpressionSyntax { Left: IdentifierNameSyntax target, Right: LiteralExpressionSyntax literal } compound
				when compound.IsKind(SyntaxKind.AddAssignmentExpression) || compound.IsKind(SyntaxKind.SubtractAssignmentExpression):
				return target.Identifier.Text == loopVar
				       && literal.IsKind(SyntaxKind.NumericLiteralExpression)
				       && Convert.ToDouble(literal.Token.Value) != 0;

			default:
				return false;
		}
	}

	private static bool StructurallyEqual(SyntaxNode first, SyntaxNode second)
	{
		return first.NormalizeWhitespace().ToFullString() == second.NormalizeWhitespace().ToFullString();
	}
}
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Performs Loop Invariant Code Motion (LICM): hoists local-variable declarations whose
///   initializers do not depend on any variable modified inside the loop to just before the loop.
///   Only simple patterns are handled:
///   - <c>var x = expr;</c> declarations inside a loop body where <c>expr</c> contains no
///   identifier that is assigned anywhere inside the same loop body.
///   - The declaration must appear as a direct child statement of the loop's
///   immediate <see cref="BlockSyntax" /> (not nested inside an inner if/for/…).
///   Applies to <c>for</c>, <c>while</c>, <c>do-while</c> and <c>foreach</c> loops.
/// </summary>
public sealed class LoopInvariantCodeMotionRewriter : CSharpSyntaxRewriter
{
	/// <summary>
	///   Applies LICM to the supplied syntax node.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node)
	{
		var rewriter = new LoopInvariantCodeMotionRewriter();
		return rewriter.Visit(node);
	}

	// ── Block: inline hoisted declarations at the parent scope ──────

	/// <summary>
	///   Visits a block bottom-up.  For each direct child statement that is a loop,
	///   invariant declarations are extracted and inserted immediately before the loop
	///   in the same block, avoiding an extra nesting level.
	/// </summary>
	public override SyntaxNode? VisitBlock(BlockSyntax node)
	{
		// Process children first so inner loops are hoisted before outer ones.
		node = (BlockSyntax) base.VisitBlock(node)!;

		var statements = node.Statements;
		List<StatementSyntax>? result = null;

		for (var i = 0; i < statements.Count; i++)
		{
			var stmt = statements[i];
			var hoisted = TryHoistFromLoop(stmt, out var newLoop);

			if (hoisted is { Count: > 0 })
			{
				if (result is null)
				{
					result = new List<StatementSyntax>(statements.Count + hoisted.Count);

					for (var j = 0; j < i; j++)
					{
						result.Add(statements[j]);
					}
				}

				result.AddRange(hoisted);
				result.Add(newLoop!);
			}
			else
			{
				result?.Add(stmt);
			}
		}

		return result is not null ? node.WithStatements(List(result)) : node;
	}

	/// <summary>
	///   If <paramref name="stmt" /> is a loop with hoistable invariants, returns the list of
	///   hoisted declarations and sets <paramref name="newLoop" /> to the loop with the
	///   invariants removed from its body.  Returns <see langword="null" /> otherwise.
	/// </summary>
	private List<LocalDeclarationStatementSyntax>? TryHoistFromLoop(
		StatementSyntax stmt, out StatementSyntax? newLoop)
	{
		newLoop = null;

		switch (stmt)
		{
			case ForStatementSyntax { Statement: BlockSyntax forBody } forStmt:
			{
				var locals = LoopInvariance.CollectLoopLocals(forBody);

				// The for-loop's own iteration variables are loop-local too.
				if (forStmt.Declaration is { } forDecl)
				{
					foreach (var v in forDecl.Variables)
					{
						locals.Add(v.Identifier.Text);
					}
				}

				var (hoisted, newBody) = HoistInvariants(forBody, LoopInvariance.CollectWrittenInLoop(forBody), locals);

				if (hoisted.Count == 0)
				{
					return null;
				}

				newLoop = forStmt.WithStatement(newBody);
				return hoisted;
			}

			case WhileStatementSyntax { Statement: BlockSyntax whileBody } whileStmt:
			{
				var (hoisted, newBody) = HoistInvariants(whileBody, LoopInvariance.CollectWrittenInLoop(whileBody), LoopInvariance.CollectLoopLocals(whileBody));

				if (hoisted.Count == 0)
				{
					return null;
				}

				newLoop = whileStmt.WithStatement(newBody);
				return hoisted;
			}

			case DoStatementSyntax { Statement: BlockSyntax doBody } doStmt:
			{
				var (hoisted, newBody) = HoistInvariants(doBody, LoopInvariance.CollectWrittenInLoop(doBody), LoopInvariance.CollectLoopLocals(doBody));

				if (hoisted.Count == 0)
				{
					return null;
				}

				newLoop = doStmt.WithStatement(newBody);
				return hoisted;
			}

			case ForEachStatementSyntax { Statement: BlockSyntax foreachBody } foreachStmt:
			{
				// The loop variable itself is "written" on every iteration.
				var written = LoopInvariance.CollectWrittenInLoop(foreachBody);
				written.Add(foreachStmt.Identifier.Text);
				var (hoisted, newBody) = HoistInvariants(foreachBody, written, LoopInvariance.CollectLoopLocals(foreachBody));

				if (hoisted.Count == 0)
				{
					return null;
				}

				newLoop = foreachStmt.WithStatement(newBody);
				return hoisted;
			}

			default:
				return null;
		}
	}

	// ── Core helpers ──────────────────────────────────────────────────

	/// <summary>
	///   Scans the direct statements of a block and hoists those that are invariant.
	///   Returns the list of hoisted statements and the rewritten block (without the hoisted ones).
	/// </summary>
	private static (List<LocalDeclarationStatementSyntax> Hoisted, BlockSyntax NewBody) HoistInvariants(BlockSyntax body, HashSet<string> writtenInLoop, HashSet<string> loopLocals)
	{
		var hoisted = new List<LocalDeclarationStatementSyntax>();
		var remaining = new List<StatementSyntax>();

		// Names declared in this very block that have already been hoisted
		// (we need to avoid re-hoisting something that depends on an already-hoisted var).
		var alreadyHoisted = new HashSet<string>();

		foreach (var stmt in body.Statements)
		{
			if (stmt is LocalDeclarationStatementSyntax
			    {
				    Declaration.Variables: [ { Initializer.Value: { } initExpr } declarator ], IsConst: false
			    } local)
			{
				var varName = declarator.Identifier.Text;

				var identifiersInInit = new HashSet<string>(
					initExpr
						.DescendantNodesAndSelf()
						.OfType<IdentifierNameSyntax>()
						.Select(id => id.Identifier.Text));

				// A declaration is loop-invariant only when its initializer references nothing
				// written inside the loop AND the declared variable itself is never reassigned in
				// the loop. The second guard is essential: `var sum = 0;` sitting before a later
				// `sum += …` must re-run its initializer every iteration, so hoisting it out drops
				// the per-iteration reset and changes behaviour.
				var referencesWritten = identifiersInInit.Overlaps(writtenInLoop)
				                        || identifiersInInit.Overlaps(alreadyHoisted)
				                        || identifiersInInit.Overlaps(loopLocals);

				if (!referencesWritten && !writtenInLoop.Contains(varName) && LoopInvariance.IsPureExpression(initExpr))
				{
					hoisted.Add(local.WithTrailingTrivia(ElasticSpace));
					alreadyHoisted.Add(varName);

					// Skip adding the original to remaining (it was hoisted).
					continue;
				}
			}

			remaining.Add(stmt);
		}

		var newBody = body.WithStatements(List(remaining));
		return (hoisted, newBody);
	}
}
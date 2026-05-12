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
	private int _hoistedCounter;

	/// <summary>
	///   Applies LICM to the supplied syntax node.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node)
	{
		var rewriter = new LoopInvariantCodeMotionRewriter();
		return rewriter.Visit(node)!;
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
				var (hoisted, newBody) = HoistInvariants(forBody, CollectWrittenInLoop(forBody));

				if (hoisted.Count == 0)
				{
					return null;
				}

				newLoop = forStmt.WithStatement(newBody);
				return hoisted;
			}

			case WhileStatementSyntax { Statement: BlockSyntax whileBody } whileStmt:
			{
				var (hoisted, newBody) = HoistInvariants(whileBody, CollectWrittenInLoop(whileBody));

				if (hoisted.Count == 0)
				{
					return null;
				}

				newLoop = whileStmt.WithStatement(newBody);
				return hoisted;
			}

			case DoStatementSyntax { Statement: BlockSyntax doBody } doStmt:
			{
				var (hoisted, newBody) = HoistInvariants(doBody, CollectWrittenInLoop(doBody));

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
				var written = CollectWrittenInLoop(foreachBody);
				written.Add(foreachStmt.Identifier.Text);
				var (hoisted, newBody) = HoistInvariants(foreachBody, written);

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
	///   Collects the names of all variables written anywhere inside the loop body
	///   (assignments, increments, declarations with mutation potential).
	/// </summary>
	private static HashSet<string> CollectWrittenInLoop(BlockSyntax body)
	{
		var written = new HashSet<string>();

		foreach (var node in body.DescendantNodes())
		{
			switch (node)
			{
				// x = …, x += …, x -= …, etc.
				case AssignmentExpressionSyntax { Left: IdentifierNameSyntax id }:
					written.Add(id.Identifier.Text);
					break;

				// x++, x--
				case PostfixUnaryExpressionSyntax
					{
						Operand: IdentifierNameSyntax pid
					} pue when pue.IsKind(SyntaxKind.PostIncrementExpression)
					           || pue.IsKind(SyntaxKind.PostDecrementExpression):
					written.Add(pid.Identifier.Text);
					break;

				// ++x, --x
				case PrefixUnaryExpressionSyntax
					{
						Operand: IdentifierNameSyntax preid
					} prue when prue.IsKind(SyntaxKind.PreIncrementExpression)
					            || prue.IsKind(SyntaxKind.PreDecrementExpression):
					written.Add(preid.Identifier.Text);
					break;
			}
		}

		return written;
	}

	/// <summary>
	///   Determines whether an expression is "pure" (no side effects): it only contains
	///   identifiers, literals, member-access chains, invocations, binary/unary expressions,
	///   casts, and element-access expressions — nothing that mutates state.
	/// </summary>
	private static bool IsPureExpression(ExpressionSyntax expr)
	{
		foreach (var node in expr.DescendantNodesAndSelf())
		{
			switch (node)
			{
				// Safe leaf and structural nodes
				case LiteralExpressionSyntax:
				case IdentifierNameSyntax:
				case MemberAccessExpressionSyntax:
				case BinaryExpressionSyntax:
				case PrefixUnaryExpressionSyntax pue when !pue.IsKind(SyntaxKind.PreIncrementExpression)
				                                          && !pue.IsKind(SyntaxKind.PreDecrementExpression):
				case PostfixUnaryExpressionSyntax poue when !poue.IsKind(SyntaxKind.PostIncrementExpression)
				                                            && !poue.IsKind(SyntaxKind.PostDecrementExpression):
				case CastExpressionSyntax:
				case ParenthesizedExpressionSyntax:
				case InvocationExpressionSyntax:
				case ArgumentListSyntax:
				case ArgumentSyntax { RefOrOutKeyword.RawKind: 0 }:
				case ElementAccessExpressionSyntax:
				case BracketedArgumentListSyntax:
				case ConditionalExpressionSyntax:
				case TypeSyntax:
					continue;

				default:
					return false;
			}
		}

		return true;
	}

	/// <summary>
	///   Scans the direct statements of a block and hoists those that are invariant.
	///   Returns the list of hoisted statements and the rewritten block (without the hoisted ones).
	/// </summary>
	private (List<LocalDeclarationStatementSyntax> Hoisted, BlockSyntax NewBody)
		HoistInvariants(BlockSyntax body, HashSet<string> writtenInLoop)
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
				    Declaration: { Variables: [ { Initializer.Value: { } initExpr } declarator ] } decl
			    } local
			    && !local.IsConst)
			{
				var varName = declarator.Identifier.Text;

				// Only hoist if the expression type is not `var` inferred from a loop-body call,
				// i.e. if it is truly pure and does not reference any loop-written variable.
				var identifiersInInit = new HashSet<string>(
					initExpr
						.DescendantNodesAndSelf()
						.OfType<IdentifierNameSyntax>()
						.Select(id => id.Identifier.Text));

				var referencesWritten = identifiersInInit.Overlaps(writtenInLoop)
				                        || identifiersInInit.Overlaps(alreadyHoisted);

				if (!referencesWritten && IsPureExpression(initExpr))
				{
					// Rename to avoid collisions when the variable was declared inside the loop
					// (its scope would otherwise change after hoisting).
					var hoistedName = varName;

					// Keep original name when it does not shadow anything; the hoisted var
					// lives in the enclosing scope and that is safe as long as the name is
					// not already used in written set.
					if (writtenInLoop.Contains(varName))
					{
						hoistedName = $"_licm_{varName}_{_hoistedCounter++}";
					}

					LocalDeclarationStatementSyntax hoistedDecl;

					if (hoistedName != varName)
					{
						// Rename the declarator identifier.
						var renamedDeclarator = declarator.WithIdentifier(Identifier(hoistedName));
						var renamedDecl = decl.WithVariables(SeparatedList([ renamedDeclarator ]));
						hoistedDecl = local.WithDeclaration(renamedDecl);
					}
					else
					{
						hoistedDecl = local;
					}

					hoisted.Add(hoistedDecl.WithTrailingTrivia(ElasticSpace));
					alreadyHoisted.Add(varName);

					// If we renamed, add a replacement statement inside the loop that uses the new name.
					if (hoistedName != varName)
					{
						// var <varName> = <hoistedName>;  — keeps usages inside the loop working
						var innerDecl = LocalDeclarationStatement(
							VariableDeclaration(IdentifierName("var"))
								.WithVariables(SeparatedList(new[]
								{
									VariableDeclarator(Identifier(varName))
										.WithInitializer(
											EqualsValueClause(IdentifierName(hoistedName)))
								})));
						remaining.Add(innerDecl);
					}

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
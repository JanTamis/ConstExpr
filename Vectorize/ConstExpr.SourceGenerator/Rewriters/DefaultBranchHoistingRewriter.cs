using System.Collections.Generic;
using System.Linq;
using ConstExpr.SourceGenerator.Helpers;
using ConstExpr.SourceGenerator.Models;
using ConstExpr.SourceGenerator.Refactorers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Turns <c>T x1, x2; if (cond) { x1 = e1; x2 = e2; } else { ... }</c> into
///   <c>var x1 = e1; var x2 = e2; if (!cond) { ... }</c> when the <c>then</c> branch is nothing
///   but a straight-line assignment of every just-declared local to a side-effect-free value.
///   <para>
///     Safe because the <c>then</c> branch's assignments become the default, and the negated
///     condition guards exactly the code path (the original <c>else</c>) that would otherwise
///     overwrite them — unchanged, so whatever it reads or writes internally still behaves the
///     same. What makes it unsafe in general, and is checked for below: the assignments' right-hand
///     sides must be pure (hoisting means they may now run even when the original branch would not
///     have), and the surviving <c>else</c> branch must not read a hoisted variable before writing
///     it itself (that would observe the just-hoisted default instead of the pre-transform undefined
///     state).
///   </para>
/// </summary>
public static class DefaultBranchHoistingRewriter
{
	public static SyntaxNode Apply(SyntaxNode body, IDictionary<string, VariableItem> variables)
	{
		return new Rewriter(variables).Visit(body);
	}

	private sealed class Rewriter(IDictionary<string, VariableItem> variables) : CSharpSyntaxRewriter
	{
		public override SyntaxNode? VisitBlock(BlockSyntax node)
		{
			if (base.VisitBlock(node) is not BlockSyntax visited)
			{
				return null;
			}

			return visited.WithStatements(List(Merge(visited.Statements)));
		}

		private List<StatementSyntax> Merge(SyntaxList<StatementSyntax> statements)
		{
			var result = new List<StatementSyntax>();

			for (var i = 0; i < statements.Count; i++)
			{
				if (statements[i] is LocalDeclarationStatementSyntax { Declaration.Variables: { Count: > 0 } declarators } declStmt
				    && declarators.All(v => v.Initializer is null)
				    && i + 1 < statements.Count
				    && statements[i + 1] is IfStatementSyntax { Else: { } elseClause } ifStmt
				    && IsSafeToHoist(ifStmt.Condition)
				    && TryGetTrivialAssignments(ifStmt.Statement, new HashSet<string>(declarators.Select(v => v.Identifier.Text)), out var thenAssignments)
				    && IsFreeOfReadsOf(elseClause.Statement, thenAssignments.Keys))
				{
					foreach (var declarator in declarators)
					{
						var rhs = thenAssignments[declarator.Identifier.Text];
						var type = InferredDeclarationType(declStmt.Declaration.Type, rhs);

						result.Add(LocalDeclarationStatement(
							VariableDeclaration(type)
								.WithVariables(SingletonSeparatedList(
									VariableDeclarator(declarator.Identifier)
										.WithInitializer(EqualsValueClause(rhs))))));
					}

					var prunedElse = RemoveRedundantReassignments(elseClause.Statement, thenAssignments);

					result.Add(IfStatement(NegateExpressionRefactoring.Negate(ifStmt.Condition, false), prunedElse));

					i++; // the if statement was folded into the loop above, skip it
					continue;
				}

				result.Add(statements[i]);
			}

			return result;
		}

		/// <summary>
		///   Once the <c>then</c> branch's assignments become each local's initializer, any assignment
		///   inside the surviving <c>else</c> branch that re-assigns the exact same value is now
		///   redundant — the local already holds it. Removes the first such redundant assignment on
		///   each execution path (switch sections and if/else arms are independent paths; loops are
		///   left untouched since a prior iteration may already have changed the value).
		/// </summary>
		private static StatementSyntax RemoveRedundantReassignments(StatementSyntax branch, IReadOnlyDictionary<string, ExpressionSyntax> assignments)
		{
			return ProcessBranch(branch, assignments, new List<StatementSyntax>());
		}

		private static StatementSyntax ProcessBranch(StatementSyntax branch, IReadOnlyDictionary<string, ExpressionSyntax> assignments, List<StatementSyntax> prefix)
		{
			switch (branch)
			{
				case BlockSyntax block:
				{
					return block.WithStatements(List(ProcessStatements(block.Statements, assignments, prefix)));
				}

				case IfStatementSyntax ifStmt:
				{
					var updated = ifStmt.WithStatement(ProcessBranch(ifStmt.Statement, assignments, new List<StatementSyntax>(prefix)));

					if (ifStmt.Else is { } elseClause)
					{
						updated = updated.WithElse(elseClause.WithStatement(ProcessBranch(elseClause.Statement, assignments, new List<StatementSyntax>(prefix))));
					}

					return updated;
				}

				case SwitchStatementSyntax switchStmt:
				{
					return switchStmt.WithSections(List(switchStmt.Sections.Select(section =>
						section.WithStatements(List(ProcessStatements(section.Statements, assignments, new List<StatementSyntax>(prefix)))))));
				}

				// Loops re-execute; a value known redundant before the first iteration need not stay so
				// on later ones, so removing an assignment here would be unsound. Leave as-is.
				default:
				{
					return branch;
				}
			}
		}

		private static List<StatementSyntax> ProcessStatements(SyntaxList<StatementSyntax> statements, IReadOnlyDictionary<string, ExpressionSyntax> assignments, List<StatementSyntax> prefix)
		{
			var result = new List<StatementSyntax>();

			foreach (var statement in statements)
			{
				if (statement is ExpressionStatementSyntax
				    {
					    Expression: AssignmentExpressionSyntax { RawKind: (int) SyntaxKind.SimpleAssignmentExpression, Left: IdentifierNameSyntax id } assign
				    }
				    && assignments.TryGetValue(id.Identifier.Text, out var expectedRhs)
				    && assign.Right.IsEquivalentTo(expectedRhs, false)
				    && IsUnwrittenSoFar(prefix, id.Identifier.Text, expectedRhs))
				{
					prefix.Add(statement);
					continue;
				}

				result.Add(ProcessBranch(statement, assignments, prefix));
				prefix.Add(statement);
			}

			return result;
		}

		/// <summary>
		///   True when neither <paramref name="name" /> nor any identifier <paramref name="rhs" /> reads
		///   has been written (including compound assignment, increment, or ref/out capture) in any
		///   statement seen so far on this execution path.
		/// </summary>
		private static bool IsUnwrittenSoFar(List<StatementSyntax> prefix, string name, ExpressionSyntax rhs)
		{
			var names = new HashSet<string> { name };
			names.UnionWith(rhs.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Select(id => id.Identifier.Text));

			var collector = new VariableUsageCollector(names);

			foreach (var statement in prefix)
			{
				collector.Visit(statement);
			}

			return names.All(n => collector.GetWriteCount(n) == 0 && collector.GetRefCount(n) == 0);
		}

		/// <summary>
		///   Matches a branch that is nothing but one simple assignment per name in <paramref name="declaredNames" />
		///   (no other statements, no repeats, no missing names), each assigning a side-effect-free value.
		/// </summary>
		private static bool TryGetTrivialAssignments(StatementSyntax branch, ISet<string> declaredNames, out Dictionary<string, ExpressionSyntax> assignments)
		{
			assignments = new Dictionary<string, ExpressionSyntax>();

			var statements = branch is BlockSyntax block ? block.Statements : SingletonList(branch);

			foreach (var statement in statements)
			{
				if (statement is not ExpressionStatementSyntax
				    {
					    Expression: AssignmentExpressionSyntax { RawKind: (int) SyntaxKind.SimpleAssignmentExpression, Left: IdentifierNameSyntax id } assign
				    })
				{
					return false;
				}

				var name = id.Identifier.Text;

				if (!declaredNames.Contains(name) || assignments.ContainsKey(name) || !IsSafeToHoist(assign.Right))
				{
					return false;
				}

				assignments[name] = assign.Right;
			}

			return assignments.Count == declaredNames.Count;
		}

		/// <summary>
		///   True when none of <paramref name="names" /> is read inside <paramref name="branch" /> other
		///   than as the target of a plain assignment (which just overwrites the hoisted default again).
		/// </summary>
		private static bool IsFreeOfReadsOf(StatementSyntax branch, IEnumerable<string> names)
		{
			var nameSet = new HashSet<string>(names);

			var assignmentTargets = new HashSet<SyntaxNode>(branch.DescendantNodesAndSelf()
				.OfType<AssignmentExpressionSyntax>()
				.Where(a => a.IsKind(SyntaxKind.SimpleAssignmentExpression))
				.Select(a => a.Left));

			return branch.DescendantNodesAndSelf()
				.OfType<IdentifierNameSyntax>()
				.Where(id => nameSet.Contains(id.Identifier.Text))
				.All(assignmentTargets.Contains);
		}

		/// <summary>
		///   <c>var</c> is only safe when the hoisted right-hand side infers to the exact type that was
		///   declared — otherwise (an unrecognised expression shape, or a literal/identifier whose type
		///   doesn't match) the explicit declared type is kept. See <see cref="VarDeclarationTypeGuard" />.
		/// </summary>
		private TypeSyntax InferredDeclarationType(TypeSyntax declaredType, ExpressionSyntax rhs)
		{
			return VarDeclarationTypeGuard.CanSafelyInferVar(declaredType, rhs, variables) ? ParseTypeName("var") : declaredType;
		}

		/// <summary>
		///   Side-effect-free and safe to evaluate unconditionally (may now run even on the branch
		///   that previously skipped it): literals, plain identifiers, and arithmetic/equality
		///   combinations of those. Deliberately excludes invocations, indexers, and member access —
		///   unlike <see cref="LoopInvariance.IsPureExpression" />, nothing here re-evaluates the
		///   expression elsewhere; a call moved from conditional to unconditional would be a new call.
		/// </summary>
		private static bool IsSafeToHoist(ExpressionSyntax expression)
		{
			return expression switch
			{
				LiteralExpressionSyntax => true,
				IdentifierNameSyntax => true,
				ParenthesizedExpressionSyntax paren => IsSafeToHoist(paren.Expression),
				PrefixUnaryExpressionSyntax { RawKind: (int) SyntaxKind.UnaryMinusExpression or (int) SyntaxKind.UnaryPlusExpression } unary => IsSafeToHoist(unary.Operand),
				BinaryExpressionSyntax
				{
					RawKind: (int) SyntaxKind.AddExpression
					or (int) SyntaxKind.SubtractExpression
					or (int) SyntaxKind.MultiplyExpression
					or (int) SyntaxKind.DivideExpression
					or (int) SyntaxKind.ModuloExpression
					or (int) SyntaxKind.EqualsExpression
					or (int) SyntaxKind.NotEqualsExpression
				} binary => IsSafeToHoist(binary.Left) && IsSafeToHoist(binary.Right),
				_ => false
			};
		}
	}
}
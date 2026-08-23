using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Sinks a local declared immediately before a <c>switch</c> statement into just the sections
///   that actually read it, when it is read in some but not all of the switch's sections and
///   nowhere else in the enclosing block:
///   <code>
///   var p = v * (1.0 - s);                  switch (i)
///   var t = v * (1.0 - s * (1.0 - f));       {
///   switch (i)                                   case 0: { var t = v * (1.0 - s * (1.0 - f)); r = v; g = t; b = p; break; }
///   {                                 =>          case 3: { var p = v * (1.0 - s); r = p; g = q; b = v; break; }
///       case 0: r = v; g = t; b = p; break;       ...
///       case 3: r = p; g = q; b = v; break;   }
///       ...
///   }
///   </code>
///   <para>
///     Only ever removes evaluations, never adds one: on every path that used to read the sunk
///     local, it still runs exactly once (now inside the section instead of before the switch); on
///     a path whose section never read it, it now runs zero times instead of one. That makes this
///     safe even for an expression that can throw — the throw is now correctly gated behind the
///     same condition that determines whether the value is ever read, never the reverse.
///   </para>
///   <para>
///     A candidate referenced by <em>another</em> pre-switch declaration in the same cluster, by the
///     switch's own governing expression, or by any statement after the switch is left hoisted —
///     sinking it would either strand a dangling reference or move an evaluation the un-sunk reader
///     still needs unconditionally. This also means candidates never need dependency ordering: one
///     referenced by another candidate is disqualified by this same rule, so whatever a sunk
///     candidate's own initializer references is guaranteed to still be in scope — either an outer
///     local/parameter, or another declaration this pass left exactly where it was.
///   </para>
///   <para>
///     A switch with any pattern label (<c>when</c>-clauses included) is skipped outright: deciding
///     which section runs could itself depend on a candidate's value, which is exactly the ordering
///     this pass must not create.
///   </para>
/// </summary>
public static class SwitchCaseSinkingRewriter
{
	public static SyntaxNode Apply(SyntaxNode body)
	{
		return new Rewriter().Visit(body)!;
	}

	private sealed class Rewriter : CSharpSyntaxRewriter
	{
		public override SyntaxNode? VisitBlock(BlockSyntax node)
		{
			// Bottom-up: a nested switch inside an outer switch's section is sunk into first, so the
			// outer pass sees its final (already up-to-date) statement shape.
			if (base.VisitBlock(node) is not BlockSyntax visited)
			{
				return null;
			}

			return visited.WithStatements(List(Sink(visited.Statements)));
		}

		private static List<StatementSyntax> Sink(SyntaxList<StatementSyntax> statements)
		{
			var result = new List<StatementSyntax>();

			for (var i = 0; i < statements.Count; i++)
			{
				if (statements[i] is not SwitchStatementSyntax { Sections.Count: >= 2 } switchStmt
				    || switchStmt.Sections.Any(s => s.Labels.Any(l => l is CasePatternSwitchLabelSyntax)))
				{
					result.Add(statements[i]);
					continue;
				}

				// The contiguous run of single-variable, initialized, non-ref/const local declarations
				// immediately preceding this switch. `result` mirrors statements[0..i-1] here: any
				// earlier switch in this block that sank part of its own cluster removed only
				// declarations separated from this one by that earlier switch statement itself, so it
				// never affects adjacency to this switch.
				var clusterStart = result.Count;

				while (clusterStart > 0 && IsSinkableDeclarationShape(result[clusterStart - 1]))
				{
					clusterStart--;
				}

				if (clusterStart == result.Count)
				{
					result.Add(statements[i]);
					continue;
				}

				var cluster = result.Skip(clusterStart).Cast<LocalDeclarationStatementSyntax>().ToList();
				var rest = statements.Skip(i + 1).ToList();
				var updatedSwitch = switchStmt;
				var sunkNames = new HashSet<string>();

				foreach (var decl in cluster)
				{
					var name = decl.Declaration.Variables[0].Identifier.Text;
					var initializer = decl.Declaration.Variables[0].Initializer!.Value;

					if (!IsSafeToSink(initializer)
					    || cluster.Any(other => !ReferenceEquals(other, decl) && ReferencesName(other.Declaration.Variables[0].Initializer!.Value, name))
					    || ReferencesName(switchStmt.Expression, name)
					    || rest.Any(s => s.HasIdentifier(name)))
					{
						continue;
					}

					var usingSections = updatedSwitch.Sections
						.Where(s => s.Statements.Any(st => st.HasIdentifier(name)))
						.ToList();

					// Used in none (dead — not this pass's job) or in every section (already free, since
					// it would run exactly once either way): no benefit to sinking.
					if (usingSections.Count == 0 || usingSections.Count == updatedSwitch.Sections.Count)
					{
						continue;
					}

					// A section that already declares the same name (a real shadow, how ever unlikely)
					// cannot safely receive a second declaration of it.
					if (usingSections.Any(s => s.Statements.Any(st => st.DescendantNodesAndSelf()
						    .OfType<VariableDeclaratorSyntax>().Any(v => v.Identifier.Text == name))))
					{
						continue;
					}

					var sectionSet = new HashSet<SwitchSectionSyntax>(usingSections);

					updatedSwitch = updatedSwitch.WithSections(List(updatedSwitch.Sections
						.Select(s => sectionSet.Contains(s) ? WithSunkDeclaration(s, decl) : s)));

					sunkNames.Add(name);
				}

				if (sunkNames.Count > 0)
				{
					result.RemoveRange(clusterStart, cluster.Count);
					result.AddRange(cluster.Where(d => !sunkNames.Contains(d.Declaration.Variables[0].Identifier.Text)));
				}

				result.Add(updatedSwitch);
			}

			return result;
		}

		/// <summary>
		///   Prepends <paramref name="decl" /> to <paramref name="section" />'s body, reusing its
		///   existing wrapper block if an earlier candidate in the same cluster already introduced one
		///   (each switch section shares one declaration space across all its statements, so a second
		///   sunk local needs to land in that same nested scope, not a fresh sibling one).
		/// </summary>
		private static SwitchSectionSyntax WithSunkDeclaration(SwitchSectionSyntax section, LocalDeclarationStatementSyntax decl)
		{
			if (section.Statements is [ BlockSyntax block ])
			{
				return section.WithStatements(SingletonList<StatementSyntax>(block.WithStatements(block.Statements.Insert(0, decl))));
			}

			return section.WithStatements(SingletonList<StatementSyntax>(Block(List(new StatementSyntax[] { decl }.Concat(section.Statements)))));
		}

		private static bool IsSinkableDeclarationShape(StatementSyntax statement)
		{
			return statement is LocalDeclarationStatementSyntax
			{
				Modifiers.Count: 0,
				Declaration: { Type: not RefTypeSyntax, Variables: [ { Initializer: not null } ] }
			};
		}

		private static bool ReferencesName(ExpressionSyntax expr, string name)
		{
			return expr is IdentifierNameSyntax id && id.Identifier.Text == name || expr.HasIdentifier(name);
		}

		/// <summary>
		///   Side-effect-shaped enough to be worth excluding: no assignment, no increment/decrement, no
		///   lambda (the same <c>var</c>-inference hazard <see cref="CommonSubexpressionEliminator" />
		///   excludes them for). Otherwise permissive about invocations for the same reason that pass
		///   is: by the time this rewriter runs, no <see cref="SemanticModel" /> resolves against the
		///   rebuilt tree, so a real purity-by-symbol check isn't available, and this codebase already
		///   extends that same trust level to CSE. It's a strictly safer trust here than there — sinking
		///   only ever turns an unconditional evaluation into a conditional one, never the reverse, so a
		///   call that used to run unconditionally can now only run as often or less.
		/// </summary>
		private static bool IsSafeToSink(ExpressionSyntax expr)
		{
			expr = expr is ParenthesizedExpressionSyntax paren ? paren.Expression : expr;

			return expr switch
			{
				LiteralExpressionSyntax => true,
				IdentifierNameSyntax => true,
				PredefinedTypeSyntax => true,
				ThisExpressionSyntax => true,
				MemberAccessExpressionSyntax ma => IsSafeToSink(ma.Expression),
				ElementAccessExpressionSyntax ea => IsSafeToSink(ea.Expression) && ea.ArgumentList.Arguments.All(a => IsSafeToSink(a.Expression)),
				CastExpressionSyntax cast => IsSafeToSink(cast.Expression),
				PrefixUnaryExpressionSyntax
				{
					RawKind: (int) SyntaxKind.UnaryMinusExpression or (int) SyntaxKind.UnaryPlusExpression
					or (int) SyntaxKind.BitwiseNotExpression or (int) SyntaxKind.LogicalNotExpression
				} prefix => IsSafeToSink(prefix.Operand),
				BinaryExpressionSyntax binary when !binary.IsKind(SyntaxKind.AsExpression) => IsSafeToSink(binary.Left) && IsSafeToSink(binary.Right),
				ConditionalExpressionSyntax cond => IsSafeToSink(cond.Condition) && IsSafeToSink(cond.WhenTrue) && IsSafeToSink(cond.WhenFalse),
				// Excludes a lambda argument the same way CommonSubexpressionEliminator.ShouldConsider
				// does; no check on the callee's own purity, at the same trust level that pass documents.
				InvocationExpressionSyntax invocation => !invocation.DescendantNodes().Any(n => n is LambdaExpressionSyntax or AnonymousFunctionExpressionSyntax)
				                                         && IsSafeToSink(invocation.Expression) && invocation.ArgumentList.Arguments.All(a => IsSafeToSink(a.Expression)),
				_ => false
			};
		}
	}
}
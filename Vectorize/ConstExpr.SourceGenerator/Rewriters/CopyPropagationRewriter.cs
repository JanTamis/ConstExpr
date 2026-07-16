using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Performs Copy Propagation: a local declared as a plain copy of another variable
///   (<c>var y = x;</c>) has its later reads replaced by the source variable, so downstream
///   passes (CSE, LICM) see one canonical name instead of two:
///   <code>
///   var y = x; … y + 1 … y + 1 …
///   =>
///   var y = x; … x + 1 … x + 1 …
///   </code>
///   The now-dead copy declaration is left in place — the DeadCodePruner run that follows every
///   pipeline pass removes it. Scope is deliberately narrow (v1): the declaration must be a
///   modifier-free <c>var</c> with a bare-identifier initializer (so copy and source share the
///   exact type), and in every statement after the copy neither variable may be written,
///   ref/out-captured, aliased via <c>ref</c>, or shadowed by a redeclaration. Because the
///   source is never written after the copy, propagating into loops and lambdas is safe —
///   precisely the case the single-use inliner refuses.
/// </summary>
public sealed class CopyPropagationRewriter : CSharpSyntaxRewriter
{
	/// <summary>
	///   Applies copy propagation to the supplied syntax node.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node)
	{
		return new CopyPropagationRewriter().Visit(node);
	}

	public override SyntaxNode? VisitBlock(BlockSyntax node)
	{
		// Recurse first so nested blocks propagate their own copies before this block is scanned.
		var visited = (BlockSyntax) base.VisitBlock(node)!;
		var statements = visited.Statements;

		// Top-down so chains (`var a = x; var b = a;`) collapse to the root source in one sweep.
		for (var i = 0; i < statements.Count; i++)
		{
			if (statements[i] is not LocalDeclarationStatementSyntax
			    {
				    Modifiers.Count: 0,
				    UsingKeyword.RawKind: 0,
				    Declaration:
				    {
					    Type.IsVar: true,
					    Variables: [ { Initializer.Value: IdentifierNameSyntax sourceId } declarator ]
				    }
			    })
			{
				continue;
			}

			var copy = declarator.Identifier.Text;
			var source = sourceId.Identifier.Text;

			if (copy == source || !CanPropagate(statements, i + 1, copy, source))
			{
				continue;
			}

			var replacer = new ReadReplacer(copy, source);

			for (var j = i + 1; j < statements.Count; j++)
			{
				statements = statements.Replace(statements[j], (StatementSyntax) replacer.Visit(statements[j])!);
			}
		}

		return visited.WithStatements(statements);
	}

	/// <summary>
	///   A copy may only be propagated when, in every statement after its declaration, neither the
	///   copy nor the source is written, ref/out-captured, ref-aliased, or shadowed — then every
	///   read of the copy is guaranteed to observe the value the source still holds.
	/// </summary>
	private static bool CanPropagate(SyntaxList<StatementSyntax> statements, int start, string copy, string source)
	{
		for (var i = start; i < statements.Count; i++)
		{
			var statement = statements[i];
			var collector = new VariableUsageCollector([ copy, source ]);
			collector.Visit(statement);

			if (collector.GetWriteCount(copy) > 0 || collector.GetRefCount(copy) > 0
			                                      || collector.GetWriteCount(source) > 0 || collector.GetRefCount(source) > 0)
			{
				return false;
			}

			foreach (var descendant in statement.DescendantNodes())
			{
				switch (descendant)
				{
					// A redeclaration of either name changes what the identifier means from that
					// point on — bail out instead of reasoning about scopes.
					case VariableDeclaratorSyntax redeclared when redeclared.Identifier.Text == copy || redeclared.Identifier.Text == source:
					case SingleVariableDesignationSyntax designation when designation.Identifier.Text == copy || designation.Identifier.Text == source:
						return false;

					// `ref var z = ref y` creates a writable alias the usage collector cannot see.
					case RefExpressionSyntax refExpression when refExpression.DescendantNodesAndSelf()
						.OfType<IdentifierNameSyntax>()
						.Any(id => id.Identifier.Text == copy || id.Identifier.Text == source):
						return false;
				}
			}
		}

		return true;
	}

	/// <summary>
	///   Replaces value reads of one identifier with another, skipping name positions that are not
	///   reads of the local (member names, argument labels, object-initializer names).
	/// </summary>
	private sealed class ReadReplacer(string from, string to) : CSharpSyntaxRewriter
	{
		public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
		{
			if (node.Identifier.Text != from)
			{
				return node;
			}

			if (node.Parent is MemberAccessExpressionSyntax member && member.Name == node
			    || node.Parent is MemberBindingExpressionSyntax or NameColonSyntax or NameEqualsSyntax)
			{
				return node;
			}

			return IdentifierName(to).WithTriviaFrom(node);
		}
	}
}
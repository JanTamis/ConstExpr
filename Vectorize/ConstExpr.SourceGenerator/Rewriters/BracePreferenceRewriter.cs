using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Helpers;

namespace ConstExpr.SourceGenerator.Rewriters;

using static SyntaxFactory;

/// <summary>
///   Wraps brace-less embedded statements in a block when <c>csharp_prefer_braces = true</c>.
/// </summary>
/// <remarks>
///   <para>
///     Runs <em>before</em> <c>NormalizeWhitespace</c>: a freshly created <see cref="BlockSyntax" />
///     carries no trivia at all, so it has to be laid out by the normalizer afterwards. Running it
///     after normalization would render <c>{return input;}</c> on a single line.
///   </para>
///   <para>
///     <see cref="BracePreference.WhenMultiline" /> is deliberately not handled here. Before
///     normalization a synthesized statement has no trivia and therefore always looks single-line,
///     so the question cannot be answered yet; <see cref="BlockFormattingRewriter" /> decides it
///     after normalization instead, where the line structure is real.
///   </para>
/// </remarks>
public sealed class BracePreferenceRewriter : CSharpSyntaxRewriter
{
	/// <summary>
	///   Applies the rewriter, or returns <paramref name="node" /> untouched when the configured
	///   preference does not call for adding braces.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode node, FormattingOptions options)
	{
		return options.PreferBraces == BracePreference.Always
			? new BracePreferenceRewriter().Visit(node)
			: node;
	}

	public override SyntaxNode? VisitIfStatement(IfStatementSyntax node)
	{
		var visited = base.VisitIfStatement(node) as IfStatementSyntax;

		return visited?.WithStatement(EnsureBlock(visited.Statement)) ?? visited;
	}

	public override SyntaxNode? VisitElseClause(ElseClauseSyntax node)
	{
		var visited = base.VisitElseClause(node) as ElseClauseSyntax;

		// An "else if" chain is not a brace-less body; leave it as-is.
		if (visited is null || visited.Statement is IfStatementSyntax)
		{
			return visited;
		}

		return visited.WithStatement(EnsureBlock(visited.Statement));
	}

	public override SyntaxNode? VisitForStatement(ForStatementSyntax node)
	{
		var visited = base.VisitForStatement(node) as ForStatementSyntax;

		return visited?.WithStatement(EnsureBlock(visited.Statement)) ?? visited;
	}

	public override SyntaxNode? VisitForEachStatement(ForEachStatementSyntax node)
	{
		var visited = base.VisitForEachStatement(node) as ForEachStatementSyntax;

		return visited?.WithStatement(EnsureBlock(visited.Statement)) ?? visited;
	}

	public override SyntaxNode? VisitForEachVariableStatement(ForEachVariableStatementSyntax node)
	{
		var visited = base.VisitForEachVariableStatement(node) as ForEachVariableStatementSyntax;

		return visited?.WithStatement(EnsureBlock(visited.Statement)) ?? visited;
	}

	public override SyntaxNode? VisitWhileStatement(WhileStatementSyntax node)
	{
		var visited = base.VisitWhileStatement(node) as WhileStatementSyntax;

		return visited?.WithStatement(EnsureBlock(visited.Statement)) ?? visited;
	}

	public override SyntaxNode? VisitDoStatement(DoStatementSyntax node)
	{
		var visited = base.VisitDoStatement(node) as DoStatementSyntax;

		return visited?.WithStatement(EnsureBlock(visited.Statement)) ?? visited;
	}

	public override SyntaxNode? VisitLockStatement(LockStatementSyntax node)
	{
		var visited = base.VisitLockStatement(node) as LockStatementSyntax;

		return visited?.WithStatement(EnsureBlock(visited.Statement)) ?? visited;
	}

	public override SyntaxNode? VisitFixedStatement(FixedStatementSyntax node)
	{
		var visited = base.VisitFixedStatement(node) as FixedStatementSyntax;

		return visited?.WithStatement(EnsureBlock(visited.Statement)) ?? visited;
	}

	public override SyntaxNode? VisitUsingStatement(UsingStatementSyntax node)
	{
		var visited = base.VisitUsingStatement(node) as UsingStatementSyntax;

		// Chained "using (a) using (b) { }" statements are idiomatic without braces.
		if (visited is null || visited.Statement is UsingStatementSyntax)
		{
			return visited;
		}

		return visited.WithStatement(EnsureBlock(visited.Statement));
	}

	private static StatementSyntax EnsureBlock(StatementSyntax statement)
	{
		return statement is BlockSyntax or EmptyStatementSyntax
			? statement
			: Block(SingletonList(statement));
	}
}
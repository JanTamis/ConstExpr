using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Enumerators;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Distributes a shared positive scale factor out of a <c>Max</c>/<c>Min</c> reduction chain
///   (<c>Max(a*K, b*K) =&gt; Max(a,b) * K</c>), then — when the chain's complement (<c>Base - chain</c>)
///   is itself divided into by a numerator built from the same scaled operands — cancels the shared
///   factor out of that division too:
///   <code>
///   var dr = r * K;                    var max = Max(Max(r, g), b);
///   var dg = g * K;                    var k = 1D - max * K;
///   var db = b * K;             =&gt;
///   var k = 1D - Max(Max(dr,dg),db);
///   var c = (1D - dr - k) / (1D - k);  var c = (max - r) / max;
///   </code>
///   <para>
///     A group's rewrite is only kept when it fully eliminates every one of that group's scaled
///     locals — measured, not assumed: distributing the constant out of the chain removes the
///     multiplications the chain itself did (e.g. computing <c>Max</c> over <c>r</c>,<c>g</c>,<c>b</c>
///     instead of <c>dr</c>,<c>dg</c>,<c>db</c>), but if a scaled local is <em>also</em> read outside
///     the chain (e.g. an HSL conversion's <c>normalizedR</c>, still needed by its hue formula), that
///     local can't be deleted — the multiplication still has to happen for its other use — and the
///     rewrite only adds a second one to recover the scaled <c>min</c>/<c>max</c>. Benchmarked at
///     ~1.33x slower for exactly that shape (byte-domain comparisons don't recoup two extra
///     multiplications with nothing to offset them). So a group's whole rewrite is speculatively
///     applied, then reverted entirely unless every one of its locals ends up fully dereferenced by
///     <see cref="RemoveFullyDereferencedLocals" /> — never left half-applied.
///   </para>
///   <para>
///     Only reached when both <see cref="FastMathFlags.AssociativeMath" /> (reordering a Max/Min
///     chain's operands) and <see cref="FastMathFlags.ReciprocalMath" /> (the division cancellation
///     below assumes a fast-math-relaxed division) are enabled.
///   </para>
/// </summary>
public sealed class MaxMinScaleFactorRewriter : CSharpSyntaxRewriter
{
	private readonly HashSet<string> usedNames = new();

	public static SyntaxNode Apply(SyntaxNode node, FastMathFlags mathOptimizations)
	{
		if (!mathOptimizations.HasFlag(FastMathFlags.AssociativeMath) || !mathOptimizations.HasFlag(FastMathFlags.ReciprocalMath))
		{
			return node;
		}

		var rewriter = new MaxMinScaleFactorRewriter();
		rewriter.SeedUsedNames(node);

		return rewriter.Visit(node);
	}

	private void SeedUsedNames(SyntaxNode node)
	{
		foreach (var token in node.DescendantTokens())
		{
			if (token.IsKind(SyntaxKind.IdentifierToken))
			{
				usedNames.Add(token.ValueText);
			}
		}
	}

	private string MintName(string baseName)
	{
		var name = baseName;
		var counter = 1;

		while (usedNames.Contains(name))
		{
			name = $"{baseName}{++counter}";
		}

		usedNames.Add(name);
		return name;
	}

	public override SyntaxNode? VisitBlock(BlockSyntax node)
	{
		// Recurse first so nested blocks are rewritten in isolation (bottom-up), matching every
		// other whole-block rewriter in this pipeline.
		if (base.VisitBlock(node) is not BlockSyntax visited)
		{
			return null;
		}

		foreach (var group in FindScaledLocalGroups(visited))
		{
			visited = RewriteGroup(visited, group);
		}

		return visited;
	}

	private sealed record ScaledLocal(string Name, ExpressionSyntax ParamExpr, LiteralExpressionSyntax LiteralK);

	/// <summary>
	///   Finds local declarations of shape <c>var v = expr * literalK;</c> (K a positive numeric
	///   literal), grouped by K's rendered text — every member of a group was scaled by the exact
	///   same constant. Groups of fewer than two members can never feed a Max/Min chain of two or
	///   more distinct operands, so they're dropped.
	/// </summary>
	private static List<List<ScaledLocal>> FindScaledLocalGroups(BlockSyntax block)
	{
		var scaled = new List<ScaledLocal>();

		foreach (var statement in block.Statements)
		{
			if (TryGetScaledLocal(statement, out var local))
			{
				scaled.Add(local);
			}
		}

		return scaled
			.GroupBy(s => s.LiteralK.Token.Text)
			.Where(g => g.Count() >= 2)
			.Select(g => g.ToList())
			.ToList();
	}

	private static bool TryGetScaledLocal(StatementSyntax statement, out ScaledLocal local)
	{
		local = null!;

		if (statement is not LocalDeclarationStatementSyntax
		    {
			    Modifiers.Count: 0,
			    Declaration:
			    {
				    Type.IsVar: true,
				    Variables: [ { Initializer.Value: BinaryExpressionSyntax { RawKind: (int) SyntaxKind.MultiplyExpression, Right: LiteralExpressionSyntax literalK } mul } declarator ]
			    }
		    })
		{
			return false;
		}

		if (literalK.Token.Value is not { } value || !IsPositiveNumericLiteral(value))
		{
			return false;
		}

		local = new ScaledLocal(declarator.Identifier.Text, mul.Left, literalK);
		return true;
	}

	private static bool IsPositiveNumericLiteral(object value)
	{
		return value switch
		{
			double d => d > 0,
			float f => f > 0,
			decimal m => m > 0,
			_ => false
		};
	}

	private BlockSyntax RewriteGroup(BlockSyntax originalBlock, List<ScaledLocal> group)
	{
		var groupNames = new HashSet<string>(group.Select(g => g.Name));
		var block = originalBlock;

		// A single group of equally-scaled locals can feed more than one independent reduction over
		// the same operands (e.g. both a Min and a Max chain, as in an HSL conversion) — keep
		// rewriting until no further chain over this group remains to be found.
		while (TryFindReductionChain(block, groupNames, out var chainInvocation, out var methodKind))
		{
			block = RewriteChain(block, group, chainInvocation, methodKind);
		}

		var rewritten = RemoveFullyDereferencedLocals(block, group);

		// A scaled local still declared here is one that's read outside the chain(s) just rewritten
		// (e.g. HSL's normalizedR, still needed by its hue formula) — that multiplication has to
		// happen regardless, so distributing the constant out of the chain only adds a second one to
		// recover the scaled value, with nothing it removed to offset it against. Keeping the group's
		// rewrite in that case is a measured, not assumed, net loss (see the class doc comment) — so
		// the whole group is reverted rather than left half-applied.
		var fullyEliminated = group.All(local => rewritten.Statements
			.OfType<LocalDeclarationStatementSyntax>()
			.All(s => s.Declaration.Variables is not [ { } d ] || d.Identifier.Text != local.Name));

		return fullyEliminated ? rewritten : originalBlock;
	}

	private BlockSyntax RewriteChain(BlockSyntax block, List<ScaledLocal> group, InvocationExpressionSyntax chainInvocation, string methodKind)
	{
		var groupNames = new HashSet<string>(group.Select(g => g.Name));
		var rawChain = (ExpressionSyntax) chainInvocation.ReplaceNodes(
			chainInvocation.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Where(id => groupNames.Contains(id.Identifier.Text)),
			(orig, _) => group.First(g => g.Name == orig.Identifier.Text).ParamExpr);

		var literalK = group[0].LiteralK;
		var statements = block.Statements.ToList();

		// Case (a): the chain is the *entire* initializer of an existing named local (e.g. a
		// user-written `var max = Math.Max(...)`) — preserve that name (it may be referenced
		// elsewhere for reasons unrelated to this rewrite) and introduce a fresh local for the raw
		// (un-scaled) reduction instead of replacing it.
		var owningIndex = statements.FindIndex(s =>
			s is LocalDeclarationStatementSyntax { Declaration.Variables: [ { Initializer.Value: { } init } ] }
			&& Unparenthesize(init).IsEquivalentTo(chainInvocation));

		string rawName;

		if (owningIndex >= 0)
		{
			var owningDeclaration = (LocalDeclarationStatementSyntax) statements[owningIndex];
			var existingName = owningDeclaration.Declaration.Variables[0].Identifier.Text;
			rawName = MintName($"{existingName}Raw");

			var rawDeclaration = LocalDeclarationStatement(
				VariableDeclaration(IdentifierName("var"))
					.WithVariables(SingletonSeparatedList(
						VariableDeclarator(Identifier(rawName)).WithInitializer(EqualsValueClause(rawChain)))));

			var scaledInitializer = BinaryExpression(SyntaxKind.MultiplyExpression, IdentifierName(rawName), literalK);
			var updatedDeclaration = owningDeclaration.ReplaceNode(owningDeclaration.Declaration.Variables[0].Initializer!.Value, scaledInitializer);

			statements[owningIndex] = updatedDeclaration;
			statements.Insert(owningIndex, rawDeclaration);
		}
		else
		{
			rawName = MintName(methodKind == "Min" ? "min" : "max");

			var rawDeclaration = LocalDeclarationStatement(
				VariableDeclaration(IdentifierName("var"))
					.WithVariables(SingletonSeparatedList(
						VariableDeclarator(Identifier(rawName)).WithInitializer(EqualsValueClause(rawChain)))));

			var anchorIndex = statements.FindIndex(s => s.DescendantNodesAndSelf().Contains(chainInvocation));
			var scaledOccurrence = BinaryExpression(SyntaxKind.MultiplyExpression, IdentifierName(rawName), literalK);

			statements[anchorIndex] = statements[anchorIndex].ReplaceNode(chainInvocation, scaledOccurrence);
			statements.Insert(anchorIndex, rawDeclaration);
		}

		var rewritten = block.WithStatements(List(statements));

		return TryCancelComplementRatios(rewritten, group, rawName, literalK);
	}

	/// <summary>
	///   Finds a (possibly nested) same-named Max/Min invocation chain whose flattened leaves are
	///   exactly (one-to-one, no extras) identifier references to <paramref name="groupNames" />.
	/// </summary>
	private static bool TryFindReductionChain(BlockSyntax block, HashSet<string> groupNames, out InvocationExpressionSyntax chain, out string methodKind)
	{
		chain = null!;
		methodKind = "";

		foreach (var candidate in block.DescendantNodes().OfType<InvocationExpressionSyntax>())
		{
			if (!IsReductionCall(candidate, out _) || !IsOutermostReductionCall(candidate))
			{
				continue;
			}

			if (!TryFlatten(candidate, out var leaves, out var kind) || !LeavesMatchGroup(leaves, groupNames))
			{
				continue;
			}

			chain = candidate;
			methodKind = kind;
			return true;
		}

		return false;
	}

	/// <summary>
	///   An inner link of a chain is reached and validated as part of flattening its parent, so only
	///   the maximal (outermost) invocation of a chain is a real candidate.
	/// </summary>
	private static bool IsOutermostReductionCall(InvocationExpressionSyntax candidate)
	{
		return candidate.Parent?.Parent is not ArgumentSyntax { Parent: ArgumentListSyntax { Parent: InvocationExpressionSyntax outer } }
		       || !IsReductionCall(outer, out _);
	}

	private static bool LeavesMatchGroup(List<ExpressionSyntax> leaves, HashSet<string> groupNames)
	{
		if (leaves.Count != groupNames.Count)
		{
			return false;
		}

		var leafNames = leaves.Select(l => (l as IdentifierNameSyntax)?.Identifier.Text).ToList();

		return leafNames.All(n => n is not null) && new HashSet<string>(leafNames!).SetEquals(groupNames);
	}

	private static bool IsReductionCall(InvocationExpressionSyntax invocation, out string normalizedName)
	{
		normalizedName = "";

		if (invocation is not { Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Max" or "MaxNative" or "Min" or "MinNative" } member, ArgumentList.Arguments: [ _, _ ] })
		{
			return false;
		}

		normalizedName = member.Name.Identifier.Text is "Max" or "MaxNative" ? "Max" : "Min";
		return true;
	}

	private static bool TryFlatten(ExpressionSyntax expr, out List<ExpressionSyntax> leaves, out string kind)
	{
		var leafList = new List<ExpressionSyntax>();
		string? name = null;

		var ok = Flatten(Unparenthesize(expr));

		leaves = leafList;
		kind = name ?? "";
		return ok;

		bool Flatten(ExpressionSyntax e)
		{
			e = Unparenthesize(e);

			if (e is InvocationExpressionSyntax inv && IsReductionCall(inv, out var thisName))
			{
				if (name is null)
				{
					name = thisName;
				}
				else if (name != thisName)
				{
					return false;
				}

				var args = inv.ArgumentList.Arguments;
				return Flatten(args[0].Expression) && Flatten(args[1].Expression);
			}

			leafList.Add(e);
			return true;
		}
	}

	/// <summary>
	///   Once <paramref name="rawName" /> * <paramref name="literalK" /> replaces the reduction
	///   chain, a local <c>k = Base - (rawName * literalK)</c> may exist elsewhere in the block, with
	///   divisions of shape <c>(Base - v_i - k) / (Base - k)</c> for each scaled local <c>v_i</c> in
	///   <paramref name="group" /> (left-associative, as originally written — this pass runs before
	///   CSE's own reassociation). Cancels the shared K directly: <c>(rawName - paramExpr_i) / rawName</c>.
	/// </summary>
	private static BlockSyntax TryCancelComplementRatios(BlockSyntax block, List<ScaledLocal> group, string rawName, LiteralExpressionSyntax literalK)
	{
		if (!TryFindComplementDeclarator(block, rawName, literalK, out var complementDeclarator))
		{
			return block;
		}

		var kName = complementDeclarator.Identifier.Text;
		var baseExpr = ((BinaryExpressionSyntax) complementDeclarator.Initializer!.Value).Left;

		var replacements = new Dictionary<ExpressionSyntax, ExpressionSyntax>();

		foreach (var division in block.DescendantNodes().OfType<BinaryExpressionSyntax>().Where(b => b.IsKind(SyntaxKind.DivideExpression)))
		{
			if (!TryMatchComplementRatio(division, baseExpr, kName, group, out var v))
			{
				continue;
			}

			replacements[division] = BinaryExpression(
				SyntaxKind.DivideExpression,
				ParenthesizedExpression(BinaryExpression(SyntaxKind.SubtractExpression, IdentifierName(rawName), v.ParamExpr)),
				IdentifierName(rawName));
		}

		return replacements.Count == 0 ? block : block.ReplaceNodes(replacements.Keys, (orig, _) => replacements[orig]);
	}

	/// <summary>
	///   Finds a local declared as <c>k = Base - (rawName * literalK)</c> — the complement of the
	///   reduction chain <see cref="RewriteGroup" /> just rewrote to <c>rawName * literalK</c>.
	/// </summary>
	private static bool TryFindComplementDeclarator(BlockSyntax block, string rawName, LiteralExpressionSyntax literalK, out VariableDeclaratorSyntax declarator)
	{
		declarator = null!;

		foreach (var statement in block.Statements.OfType<LocalDeclarationStatementSyntax>())
		{
			if (statement.Declaration.Variables is not
			    [
				    {
					    Initializer.Value: BinaryExpressionSyntax
					    {
						    RawKind: (int) SyntaxKind.SubtractExpression,
						    Right: BinaryExpressionSyntax
						    {
							    RawKind: (int) SyntaxKind.MultiplyExpression,
							    Left: IdentifierNameSyntax rawRef,
							    Right: LiteralExpressionSyntax rhsLiteral
						    }
					    }
				    } candidate
			    ])
			{
				continue;
			}

			if (rawRef.Identifier.Text != rawName || !rhsLiteral.IsEquivalentTo(literalK))
			{
				continue;
			}

			declarator = candidate;
			return true;
		}

		return false;
	}

	private static bool TryMatchComplementRatio(BinaryExpressionSyntax division, ExpressionSyntax baseExpr, string kName, List<ScaledLocal> group, out ScaledLocal matched)
	{
		matched = null!;

		// Denominator: Base - k
		if (Unparenthesize(division.Right) is not BinaryExpressionSyntax { RawKind: (int) SyntaxKind.SubtractExpression, Right: IdentifierNameSyntax denomK } denom
		    || denomK.Identifier.Text != kName || !Unparenthesize(denom.Left).IsEquivalentTo(baseExpr))
		{
			return false;
		}

		// Numerator: (Base - v_i) - k, left-associative as originally written.
		if (Unparenthesize(division.Left) is not BinaryExpressionSyntax
		    {
			    RawKind: (int) SyntaxKind.SubtractExpression,
			    Left: BinaryExpressionSyntax { RawKind: (int) SyntaxKind.SubtractExpression, Right: IdentifierNameSyntax numV } inner,
			    Right: IdentifierNameSyntax numK
		    } || numK.Identifier.Text != kName || !Unparenthesize(inner.Left).IsEquivalentTo(baseExpr))
		{
			return false;
		}

		var candidate = group.FirstOrDefault(g => g.Name == numV.Identifier.Text);

		if (candidate is null)
		{
			return false;
		}

		matched = candidate;
		return true;
	}

	/// <summary>
	///   Removes a scaled local's declaration once nothing in the rewritten block references it any
	///   more. <see cref="DeadCodePruner" /> cannot be relied on for this: a local whose initializer
	///   is a compound expression (not a bare identifier/literal) is permanently marked
	///   <c>HasValue = false</c> during interpretation, and the pruner's declaration check requires
	///   <c>HasValue == true</c> regardless of read count.
	/// </summary>
	private static BlockSyntax RemoveFullyDereferencedLocals(BlockSyntax block, List<ScaledLocal> group)
	{
		foreach (var local in group)
		{
			var declaration = block.Statements
				.OfType<LocalDeclarationStatementSyntax>()
				.FirstOrDefault(s => s.Declaration.Variables is [ { } d ] && d.Identifier.Text == local.Name);

			if (declaration is null)
			{
				continue;
			}

			var stillReferenced = block.DescendantNodes()
				.OfType<IdentifierNameSyntax>()
				.Any(id => id.Identifier.Text == local.Name && !declaration.Contains(id));

			if (!stillReferenced)
			{
				block = block.RemoveNode(declaration, SyntaxRemoveOptions.KeepNoTrivia)!;
			}
		}

		return block;
	}

	private static ExpressionSyntax Unparenthesize(ExpressionSyntax expr)
	{
		while (expr is ParenthesizedExpressionSyntax paren)
		{
			expr = paren.Expression;
		}

		return expr;
	}
}
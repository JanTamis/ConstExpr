using System;
using System.Collections.Generic;
using System.Linq;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Comparers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using SourceGen.Utilities.Extensions;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   Performs Common Subexpression Elimination (CSE) by identifying repeated expressions
///   and replacing them with local variables.
/// </summary>
public sealed class CommonSubexpressionEliminator(bool allowReassociation = false) : CSharpSyntaxRewriter
{
	private static readonly IEqualityComparer<ExpressionSyntax> _comparer = new NormalizedExpressionComparer();
	private readonly HashSet<string> _usedNames = new();
	private int _cseCounter;

	private string GenerateName(ExpressionSyntax expr)
	{
		expr = Unparenthesize(expr);

		var baseName = expr switch
		{
			BinaryExpressionSyntax binary => binary.Kind() switch
			{
				SyntaxKind.AddExpression => "sum",
				SyntaxKind.SubtractExpression => "diff",
				SyntaxKind.MultiplyExpression => "prod",
				SyntaxKind.DivideExpression => "quot",
				SyntaxKind.ModuloExpression => "mod",
				SyntaxKind.LeftShiftExpression => "lshift",
				SyntaxKind.RightShiftExpression => "rshift",
				SyntaxKind.BitwiseAndExpression => "and",
				SyntaxKind.BitwiseOrExpression => "or",
				SyntaxKind.ExclusiveOrExpression => "xor",
				_ => "val"
			},
			InvocationExpressionSyntax invocation => invocation.Expression switch
			{
				IdentifierNameSyntax id => $"{SanitizeIdentifierPart(id.Identifier.Text)}Val",
				MemberAccessExpressionSyntax ma => $"{SanitizeIdentifierPart(ma.Name.Identifier.Text)}Val",
				_ => "callVal"
			},
			ElementAccessExpressionSyntax => "item",
			CastExpressionSyntax => "castVal",
			ConditionalExpressionSyntax => "condVal",
			_ => "val"
		};

		var name = baseName;
		var counter = 1;

		string SanitizeIdentifierPart(string text)
		{
			var end = 0;

			while (end < text.Length && (Char.IsLetterOrDigit(text[end]) || text[end] == '_'))
			{
				end++;
			}

			return text.Substring(0, end).ToLowerInvariant();
		}

		while (_usedNames.Contains(name))
		{
			name = $"{baseName}{++counter}";
		}

		_usedNames.Add(name);
		return name;
	}

	/// <summary>
	///   Eliminates common subexpressions from the given syntax node. When <paramref name="mathOptimizations" />
	///   includes <see cref="FastMathFlags.AssociativeMath" />, multiplication and subtraction chains are
	///   first canonicalized (see <see cref="CanonicalizeForCse" />) so subexpressions that are only equal
	///   up to reassociation (e.g. <c>a * b * c</c> vs <c>a * (b * c)</c>) can still be recognized as the
	///   same repeated subexpression by the exact-match logic below.
	/// </summary>
	public static SyntaxNode? Eliminate(SyntaxNode? node, FastMathFlags mathOptimizations = FastMathFlags.Strict)
	{
		if (node is null)
		{
			return null;
		}

		var eliminator = new CommonSubexpressionEliminator(mathOptimizations.HasFlag(FastMathFlags.AssociativeMath));
		eliminator.SeedUsedNames(node);

		return eliminator.Visit(node);
	}

	/// <summary>
	///   Seeds <see cref="_usedNames" /> with every identifier already present in the tree, so a
	///   generated CSE variable can never collide with (and redeclare / shadow) an existing local,
	///   parameter, or member — which would otherwise produce non-compiling output. Tree-wide is
	///   deliberately over-conservative; per-scope seeding isn't worth the complexity.
	/// </summary>
	private void SeedUsedNames(SyntaxNode node)
	{
		foreach (var token in node.DescendantTokens())
		{
			if (token.IsKind(SyntaxKind.IdentifierToken))
			{
				_usedNames.Add(token.ValueText);
			}
		}
	}

	public override SyntaxNode? VisitBlock(BlockSyntax node)
	{
		// First, visit nested blocks to handle them in isolation (bottom-up)
		if (base.VisitBlock(node) is not BlockSyntax visitedNode)
		{
			return null;
		}

		if (allowReassociation)
		{
			visitedNode = CanonicalizeForCse(visitedNode);
		}

		// Reset names for this block context to avoid carrying over from unrelated blocks if the instance was reused
		// (though we create a new instance per Eliminate call, VisitBlock is recursive)

		var counts = new Dictionary<ExpressionSyntax, int>(_comparer);
		var lValues = new HashSet<ExpressionSyntax>(_comparer);
		var sideEffectCalls = new HashSet<ExpressionSyntax>(_comparer);
		var mutatedNames = new HashSet<string>();
		var collector = new ExpressionCollector(counts, lValues, sideEffectCalls, mutatedNames);

		foreach (var statement in visitedNode.Statements)
		{
			collector.Visit(statement);
		}

		var candidates = counts.Where(kvp => kvp.Value > 1 && ShouldConsider(kvp.Key, lValues, sideEffectCalls, mutatedNames))
			.Select(kvp => kvp.Key)
			.OrderByDescending(c => c.DescendantNodes().Count()) // Prefer larger expressions first
			.ToList();

		if (candidates.Count == 0)
		{
			return visitedNode;
		}

		var newStatements = new List<StatementSyntax>();
		var replacementMap = new Dictionary<ExpressionSyntax, string>(_comparer);

		foreach (var statement in visitedNode.Statements)
		{
			// Identify which candidates appear in this statement for the first time
			foreach (var candidate in candidates)
			{
				if (replacementMap.ContainsKey(candidate))
				{
					continue;
				}

				if (ContainsExpression(statement, candidate))
				{
					var name = GenerateName(candidate);

					// Substitute already-hoisted candidates (e.g. "mod") that occur as a nested
					// subexpression here BEFORE registering this candidate, otherwise it would
					// immediately match itself (var castVal = castVal;).
					var initializer = (ExpressionSyntax) new ExpressionReplacementRewriter(replacementMap).Visit(Unparenthesize(candidate))!;

					replacementMap[candidate] = name;

					var declaration = LocalDeclarationStatement(
						VariableDeclaration(IdentifierName("var"))
							.WithVariables(SingletonSeparatedList(
								VariableDeclarator(Identifier(name))
									.WithInitializer(EqualsValueClause(initializer))
							))
					);
					newStatements.Add(declaration);
				}
			}

			// Rewrite the statement using the current replacement map
			var rewriter = new ExpressionReplacementRewriter(replacementMap);
			newStatements.Add((StatementSyntax) rewriter.Visit(statement));
		}

		return visitedNode.WithStatements(List(newStatements));
	}

	/// <summary>
	///   Reassociates multiplication and pure-subtraction chains so that a factor/term shared by
	///   two or more sibling chains becomes an explicit, identically-shaped subtree. Runs before
	///   the exact-match candidate collection above, which is otherwise untouched — it only ever
	///   sees chains that are already aligned. Only reached when <see cref="allowReassociation" />
	///   is set, since reordering floating-point multiplication/subtraction can change rounding.
	/// </summary>
	private static BlockSyntax CanonicalizeForCse(BlockSyntax block)
	{
		block = CanonicalizeMultiplicationFactors(block);
		block = CanonicalizeSubtractionPrefixes(block);

		return block;
	}

	/// <summary>
	///   Finds maximal multiplication chains (e.g. <c>a * b * c</c>) and, when two or more chains
	///   of the same length share at least two common factors (regardless of position), regroups
	///   every participating chain as <c>(unique factors) * (shared factors)</c> so the shared
	///   product becomes a matching subtree for the ordinary CSE pass to hoist. For example
	///   <c>255 * (1-c) * (1-k)</c> and <c>255 * (1-m) * (1-k)</c> both become
	///   <c>(1-c) * ((1-k) * 255)</c> / <c>(1-m) * ((1-k) * 255)</c>, letting the existing pass
	///   hoist <c>(1-k) * 255</c> as a single "prod" variable.
	/// </summary>
	private static BlockSyntax CanonicalizeMultiplicationFactors(BlockSyntax block)
	{
		var chains = CollectTopLevelChains(block, SyntaxKind.MultiplyExpression)
			.Select(node => (Node: (ExpressionSyntax) node, Factors: FlattenChain(node, SyntaxKind.MultiplyExpression)))
			.Where(c => c.Factors.Count >= 2)
			.ToList();

		if (chains.Count < 2)
		{
			return block;
		}

		foreach (var group in chains.GroupBy(c => c.Factors.Count))
		{
			var groupList = group.ToList();

			if (groupList.Count < 2)
			{
				continue;
			}

			var common = IntersectMultisets(groupList.Select(c => c.Factors));

			if (common.Count < 2)
			{
				continue;
			}

			// Keep non-literal (variable-derived) factors before literal scale factors, e.g.
			// `(1D - k) * 255D` rather than `255D * (1D - k)`.
			var orderedCommon = common.OrderBy(f => f is LiteralExpressionSyntax ? 1 : 0).ToList();
			var sharedProduct = BuildChain(orderedCommon, SyntaxKind.MultiplyExpression);
			var replacements = new Dictionary<ExpressionSyntax, ExpressionSyntax>();

			foreach (var (node, factors) in groupList)
			{
				var unique = RemoveMultisetOnce(factors, common);
				var rebuilt = unique.Count == 0
					? sharedProduct
					: BuildChain([ ..unique, sharedProduct ], SyntaxKind.MultiplyExpression);

				replacements[node] = rebuilt;
			}

			block = block.ReplaceNodes(replacements.Keys, (orig, _) => replacements[orig]);
		}

		return block;
	}

	/// <summary>
	///   Finds maximal pure-subtraction chains from a common base (e.g. <c>a - b - c</c>) and,
	///   when a simpler chain (base minus a single term) repeats elsewhere in the block — meaning
	///   the exact-match pass above is about to hoist it on its own — regroups any longer chain
	///   from the same base that also subtracts that term, so the shared "base - term" shape
	///   becomes an explicit, matching subtree too. For example, if <c>1D - k</c> repeats as a
	///   denominator, a numerator <c>1D - dr - k</c> becomes <c>(1D - k) - dr</c>. Subtracting a
	///   set of terms from a common base is commutative among those terms, so this never changes
	///   the result.
	/// </summary>
	private static BlockSyntax CanonicalizeSubtractionPrefixes(BlockSyntax block)
	{
		var chains = CollectTopLevelChains(block, SyntaxKind.SubtractExpression)
			.Select(node => (Node: (ExpressionSyntax) node, Flat: FlattenSubtractChain(node)))
			.ToList();

		if (chains.Count < 2)
		{
			return block;
		}

		foreach (var baseGroup in chains.GroupBy(c => c.Flat.Base, _comparer))
		{
			var groupList = baseGroup.ToList();

			// A chain with exactly one subtracted term that repeats (structurally) 2+ times for
			// this base is already going to be hoisted on its own by the exact-match pass above.
			var singleTermRepeat = groupList
				.Where(c => c.Flat.Terms.Count == 1)
				.GroupBy(c => c.Flat.Terms[0], _comparer)
				.FirstOrDefault(g => g.Count() >= 2);

			if (singleTermRepeat is null)
			{
				continue;
			}

			var refTerm = singleTermRepeat.Key;
			var replacements = new Dictionary<ExpressionSyntax, ExpressionSyntax>();

			foreach (var (node, flat) in groupList)
			{
				if (flat.Terms.Count < 2)
				{
					continue;
				}

				var index = flat.Terms.FindIndex(t => _comparer.Equals(t, refTerm));

				if (index < 0)
				{
					continue;
				}

				var remaining = flat.Terms.Where((_, i) => i != index).ToList();
				var rebuilt = BuildChain([ flat.Base, refTerm, ..remaining ], SyntaxKind.SubtractExpression);

				replacements[node] = rebuilt;
			}

			if (replacements.Count > 0)
			{
				block = block.ReplaceNodes(replacements.Keys, (orig, _) => replacements[orig]);
			}
		}

		return block;
	}

	/// <summary>
	///   Collects maximal (top-of-chain) binary expressions of <paramref name="kind" /> in the
	///   block, i.e. nodes whose parent is not itself the same kind. Stops descending into nested
	///   blocks/lambdas, matching <see cref="ExpressionCollector" />'s scoping.
	/// </summary>
	private static List<BinaryExpressionSyntax> CollectTopLevelChains(BlockSyntax block, SyntaxKind kind)
	{
		return block.Statements
			.SelectMany(statement => statement.DescendantNodesAndSelf(n => n is not BlockSyntax && n is not AnonymousFunctionExpressionSyntax))
			.OfType<BinaryExpressionSyntax>()
			.Where(b => b.IsKind(kind) && !(b.Parent is BinaryExpressionSyntax parentBinary && parentBinary.IsKind(kind)))
			.ToList();
	}

	private static List<ExpressionSyntax> FlattenChain(ExpressionSyntax expr, SyntaxKind kind)
	{
		expr = Unparenthesize(expr);

		if (expr is BinaryExpressionSyntax binary && binary.IsKind(kind))
		{
			var factors = FlattenChain(binary.Left, kind);
			factors.Add(Unparenthesize(binary.Right));
			return factors;
		}

		return [ expr ];
	}

	private static (ExpressionSyntax Base, List<ExpressionSyntax> Terms) FlattenSubtractChain(ExpressionSyntax expr)
	{
		expr = Unparenthesize(expr);

		if (expr is BinaryExpressionSyntax binary && binary.IsKind(SyntaxKind.SubtractExpression))
		{
			var (baseExpr, terms) = FlattenSubtractChain(binary.Left);
			terms.Add(Unparenthesize(binary.Right));
			return (baseExpr, terms);
		}

		return (expr, [ ]);
	}

	/// <summary>
	///   Multiset intersection (via the structural expression comparer) across all of the given
	///   factor lists.
	/// </summary>
	private static List<ExpressionSyntax> IntersectMultisets(IEnumerable<List<ExpressionSyntax>> lists)
	{
		var listArray = lists.ToList();
		var common = new List<ExpressionSyntax>();
		var consideredCounts = new List<int>();

		foreach (var candidate in listArray[0])
		{
			if (consideredCounts.Count > 0 && common.Any(c => _comparer.Equals(c, candidate)))
			{
				continue;
			}

			var minCount = listArray.Min(list => list.Count(x => _comparer.Equals(x, candidate)));

			for (var i = 0; i < minCount; i++)
			{
				common.Add(candidate);
			}

			consideredCounts.Add(minCount);
		}

		return common;
	}

	private static List<ExpressionSyntax> RemoveMultisetOnce(List<ExpressionSyntax> from, List<ExpressionSyntax> toRemove)
	{
		var result = new List<ExpressionSyntax>(from);

		foreach (var item in toRemove)
		{
			var index = result.FindIndex(x => _comparer.Equals(x, item));

			if (index >= 0)
			{
				result.RemoveAt(index);
			}
		}

		return result;
	}

	private static ExpressionSyntax BuildChain(List<ExpressionSyntax> operands, SyntaxKind kind)
	{
		var result = ParenthesizeIfLowerPrecedence(operands[0], kind);

		for (var i = 1; i < operands.Count; i++)
		{
			result = BinaryExpression(kind, result, ParenthesizeIfLowerPrecedence(operands[i], kind));
		}

		return result;
	}

	/// <summary>
	///   Wraps <paramref name="operand" /> in parentheses when it is a binary expression with
	///   lower precedence than <paramref name="outerKind" />, so printing the rebuilt chain
	///   doesn't silently change its meaning (e.g. embedding a bare <c>1D - k</c> as a factor of a
	///   multiplication must print as <c>(1D - k) * 255D</c>, not <c>1D - k * 255D</c>).
	/// </summary>
	private static ExpressionSyntax ParenthesizeIfLowerPrecedence(ExpressionSyntax operand, SyntaxKind outerKind)
	{
		if (operand is not BinaryExpressionSyntax binary)
		{
			return operand;
		}

		return GetPrecedence(binary.Kind()) < GetPrecedence(outerKind)
			? ParenthesizedExpression(operand)
			: operand;
	}

	/// <summary>
	///   Minimal precedence ranking covering the binary operators this class rebuilds chains of
	///   (multiplicative binds tighter than additive), used only to decide when a rebuilt
	///   operand needs explicit parentheses.
	/// </summary>
	private static int GetPrecedence(SyntaxKind kind)
	{
		return kind switch
		{
			SyntaxKind.MultiplyExpression or SyntaxKind.DivideExpression or SyntaxKind.ModuloExpression => 2,
			SyntaxKind.AddExpression or SyntaxKind.SubtractExpression => 1,
			_ => 0
		};
	}

	private static bool ContainsExpression(SyntaxNode root, ExpressionSyntax expression)
	{
		return root.DescendantNodesAndSelf(n => n is not BlockSyntax && n is not AnonymousFunctionExpressionSyntax)
			.OfType<ExpressionSyntax>()
			.Any(e => _comparer.Equals(e, expression));
	}

	private static bool ShouldConsider(ExpressionSyntax expr, HashSet<ExpressionSyntax> lValues, HashSet<ExpressionSyntax> sideEffectCalls, HashSet<string> mutatedNames)
	{
		expr = Unparenthesize(expr);

		// Expressions used as L-values cannot be CSE'd safely
		if (lValues.Contains(expr))
		{
			return false;
		}

		// If any identifier referenced by the expression names a variable/array/object that is
		// mutated anywhere in this block, the expression's value may change between occurrences and
		// cannot be safely CSE'd. `mutatedNames` tracks the *base identifier* of every mutation
		// channel — plain assignment, inc/dec, indexer write (`arr[i] = …`) and `ref`/`out` args —
		// so e.g. `var x = arr[k]; arr[0] = v; var y = arr[k];` no longer merges the two reads.
		if (expr.DescendantNodesAndSelf()
		    .OfType<IdentifierNameSyntax>()
		    .Any(id => mutatedNames.Contains(id.Identifier.Text)))
		{
			return false;
		}

		return expr switch
		{
			// Only consider "expensive" or complex expressions
			BinaryExpressionSyntax => true,
			// A repeated ternary (e.g. `Char.IsUpper(c) ? 'A' : 'a'`) is worth hoisting into a single
			// local. Exclude lambda-bearing conditionals for the same `var`-inference reason as invocations.
			ConditionalExpressionSyntax => !expr.DescendantNodes().Any(n => n is LambdaExpressionSyntax or AnonymousFunctionExpressionSyntax),
			// Calls that appear as expression statements are called for their side effects —
			// extracting them to a variable would elide the side effect on subsequent uses.
			InvocationExpressionSyntax invocation when sideEffectCalls.Contains(invocation) => false,
			// Avoid CSE for expressions containing lambdas, as 'var' might fail to infer the delegate type
			InvocationExpressionSyntax invocation => !invocation.DescendantNodes().Any(n => n is LambdaExpressionSyntax or AnonymousFunctionExpressionSyntax),
			MemberAccessExpressionSyntax ma => ShouldConsider(ma.Expression, lValues, sideEffectCalls, mutatedNames),
			ElementAccessExpressionSyntax => true,
			CastExpressionSyntax cast => ShouldConsider(cast.Expression, lValues, sideEffectCalls, mutatedNames),
			_ => false
		};
	}

	private static ExpressionSyntax Unparenthesize(ExpressionSyntax expr)
	{
		while (expr is ParenthesizedExpressionSyntax p)
		{
			expr = p.Expression;
		}

		return expr;
	}

	/// <summary>
	///   Peels an assignment/mutation target down to its root identifier so mutations through
	///   indexers (<c>arr[i]</c>), members (<c>obj.field</c>) or parentheses all attribute to the
	///   base variable name. Returns <c>null</c> when there is no simple root identifier (e.g.
	///   <c>this.field</c>), which the caller treats as "nothing to track".
	/// </summary>
	private static string? GetBaseIdentifier(ExpressionSyntax expr)
	{
		while (true)
		{
			switch (expr)
			{
				case ParenthesizedExpressionSyntax p: expr = p.Expression; break;
				case ElementAccessExpressionSyntax e: expr = e.Expression; break;
				case MemberAccessExpressionSyntax m: expr = m.Expression; break;
				case IdentifierNameSyntax id: return id.Identifier.Text;
				default: return null;
			}
		}
	}

	/// <summary>
	///   Structural comparer used for all CSE matching. On top of stripping parentheses it
	///   canonicalizes commutative <c>+</c>/<c>*</c> operand order (see <see cref="Canonicalize" />)
	///   for the comparison key <em>only</em> — the stored/emitted expression keeps its original
	///   form — so <c>a + b</c> and <c>b + a</c> (and <c>x * y</c> / <c>y * x</c>) are recognized as
	///   the same subexpression. <see cref="Equals" /> does a real structural comparison of the
	///   canonical forms rather than trusting the (collision-prone) hash, so a hash collision can
	///   never cause two different expressions to be merged into one CSE local.
	/// </summary>
	private class NormalizedExpressionComparer : IEqualityComparer<ExpressionSyntax>
	{
		public bool Equals(ExpressionSyntax? x, ExpressionSyntax? y)
		{
			if (ReferenceEquals(x, y))
			{
				return true;
			}

			if (x == null || y == null)
			{
				return false;
			}

			// Compare the normalized text of the canonical forms rather than the (collision-prone)
			// structural hash: a hash collision must never merge two different expressions into one
			// CSE local. NormalizeWhitespace erases the trivia the operand-swap leaves behind.
			return CanonicalText(x) == CanonicalText(y);
		}

		public int GetHashCode(ExpressionSyntax obj)
		{
			// Hash is only a bucket hint; Equals is authoritative, so the fast structural hash of the
			// canonical form is fine here (equal CanonicalText ⇒ identical structure ⇒ equal hash).
			return SyntaxNodeComparer.Get<ExpressionSyntax>().GetHashCode(Canonicalize(Unparenthesize(obj)));
		}

		private static string CanonicalText(ExpressionSyntax expr)
		{
			return Canonicalize(Unparenthesize(expr)).NormalizeWhitespace().ToFullString();
		}
	}

	/// <summary>
	///   Returns a copy of <paramref name="expr" /> in which every <c>+</c>/<c>*</c> node has its two
	///   direct operands ordered deterministically (by structural hash). Only per-node commutation —
	///   never regrouping — so <c>(a+b)+c</c> and <c>a+(b+c)</c> stay distinct (associativity is only
	///   applied under fast-math via <see cref="CanonicalizeForCse" />). Safe unconditionally because
	///   IEEE-754 addition/multiplication are commutative (bit-identical <c>a+b</c> == <c>b+a</c>).
	/// </summary>
	// ponytail: builds a fresh clone per hash/equals call; memoize by node if this ever shows up hot.
	private static ExpressionSyntax Canonicalize(ExpressionSyntax expr)
	{
		return (ExpressionSyntax) new CommutativeCanonicalizer().Visit(expr);
	}

	private sealed class CommutativeCanonicalizer : CSharpSyntaxRewriter
	{
		public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
		{
			// base.Visit canonicalizes children first (bottom-up), so operand hashes below are stable.
			var visited = (BinaryExpressionSyntax) base.VisitBinaryExpression(node)!;

			if (!visited.IsKind(SyntaxKind.AddExpression) && !visited.IsKind(SyntaxKind.MultiplyExpression))
			{
				return visited;
			}

			var comparer = SyntaxNodeComparer.Get<ExpressionSyntax>();

			if (comparer.GetHashCode(Unparenthesize(visited.Left)) > comparer.GetHashCode(Unparenthesize(visited.Right)))
			{
				return visited.WithLeft(visited.Right).WithRight(visited.Left);
			}

			return visited;
		}
	}

	private class ExpressionCollector(Dictionary<ExpressionSyntax, int> counts, HashSet<ExpressionSyntax> lValues, HashSet<ExpressionSyntax> sideEffectCalls, HashSet<string> mutatedNames) : CSharpSyntaxWalker
	{
		private void MarkMutated(ExpressionSyntax target)
		{
			if (GetBaseIdentifier(target) is { } name)
			{
				mutatedNames.Add(name);
			}
		}

		public override void VisitBlock(BlockSyntax node)
		{
			/* Don't recurse into nested blocks */
		}

		public override void VisitExpressionStatement(ExpressionStatementSyntax node)
		{
			// Invocations used as expression statements are called for side effects — mark them
			if (node.Expression is InvocationExpressionSyntax invocation)
			{
				sideEffectCalls.Add(Unparenthesize(invocation));
			}

			base.VisitExpressionStatement(node);
		}

		public override void VisitAnonymousMethodExpression(AnonymousMethodExpressionSyntax node) { }
		public override void VisitSimpleLambdaExpression(SimpleLambdaExpressionSyntax node) { }
		public override void VisitParenthesizedLambdaExpression(ParenthesizedLambdaExpressionSyntax node) { }
		public override void VisitLocalFunctionStatement(LocalFunctionStatementSyntax node) { }

		public override void VisitAssignmentExpression(AssignmentExpressionSyntax node)
		{
			lValues.Add(Unparenthesize(node.Left));
			MarkMutated(Unparenthesize(node.Left));
			base.VisitAssignmentExpression(node);
		}

		public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
		{
			if (node.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PostIncrementExpression or
			    SyntaxKind.PreDecrementExpression or SyntaxKind.PostDecrementExpression)
			{
				lValues.Add(Unparenthesize(node.Operand));
				MarkMutated(Unparenthesize(node.Operand));
			}
			base.VisitPrefixUnaryExpression(node);
		}

		public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
		{
			if (node.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PostIncrementExpression or
			    SyntaxKind.PreDecrementExpression or SyntaxKind.PostDecrementExpression)
			{
				lValues.Add(Unparenthesize(node.Operand));
				MarkMutated(Unparenthesize(node.Operand));
			}
			base.VisitPostfixUnaryExpression(node);
		}

		public override void VisitArgument(ArgumentSyntax node)
		{
			// `ref`/`out` arguments mutate the passed variable, so any expression over that base
			// identifier can change value across the call and must not be CSE'd.
			if (node.RefKindKeyword.IsKind(SyntaxKind.RefKeyword) || node.RefKindKeyword.IsKind(SyntaxKind.OutKeyword))
			{
				MarkMutated(Unparenthesize(node.Expression));
			}
			base.VisitArgument(node);
		}

		public override void Visit(SyntaxNode? node)
		{
			if (node is ExpressionSyntax expr && node is not ParenthesizedExpressionSyntax)
			{
				var normalized = Unparenthesize(expr);
				counts.TryGetValue(normalized, out var count);
				counts[normalized] = count + 1;
			}

			base.Visit(node);
		}
	}

	private class ExpressionReplacementRewriter(Dictionary<ExpressionSyntax, string> replacementMap) : CSharpSyntaxRewriter
	{
		public override SyntaxNode? Visit(SyntaxNode? node)
		{
			if (node is ExpressionSyntax expr)
			{
				// Do not replace if this is an L-value position
				if (IsLValue(expr))
				{
					return base.Visit(node);
				}

				if (replacementMap.TryGetValue(expr, out var name))
				{
					return IdentifierName(name).WithTriviaFrom(node);
				}
			}

			return base.Visit(node);
		}

		private static bool IsLValue(ExpressionSyntax node)
		{
			var current = node;

			while (current.Parent is ParenthesizedExpressionSyntax p)
			{
				current = p;
			}

			var parent = current.Parent;

			if (parent is AssignmentExpressionSyntax assignment && assignment.Left == current)
			{
				return true;
			}

			if (parent is PrefixUnaryExpressionSyntax or PostfixUnaryExpressionSyntax)
			{
				if (parent.IsKind(SyntaxKind.PreIncrementExpression,
					    SyntaxKind.PostIncrementExpression,
					    SyntaxKind.PreDecrementExpression,
					    SyntaxKind.PostDecrementExpression, SyntaxKind.PreDecrementExpression,
					    SyntaxKind.PostDecrementExpression))
				{
					return true;
				}
			}

			return false;
		}
	}
}
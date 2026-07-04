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
				IdentifierNameSyntax id => $"{id.Identifier.Text.ToLowerInvariant()}Val",
				MemberAccessExpressionSyntax ma => $"{ma.Name.Identifier.Text.ToLowerInvariant()}Val",
				_ => "callVal"
			},
			ElementAccessExpressionSyntax => "item",
			CastExpressionSyntax => "castVal",
			ConditionalExpressionSyntax => "condVal",
			_ => "val"
		};

		var name = baseName;
		var counter = 1;

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

		return eliminator.Visit(node);
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
		var collector = new ExpressionCollector(counts, lValues, sideEffectCalls);

		foreach (var statement in visitedNode.Statements)
		{
			collector.Visit(statement);
		}

		var candidates = counts.Where(kvp => kvp.Value > 1 && ShouldConsider(kvp.Key, lValues, sideEffectCalls))
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
			var currentStatement = statement;

			// Identify which candidates appear in this statement for the first time
			foreach (var candidate in candidates)
			{
				if (replacementMap.ContainsKey(candidate))
				{
					continue;
				}

				if (ContainsExpression(currentStatement, candidate))
				{
					var name = GenerateName(candidate);
					replacementMap[candidate] = name;

					var declaration = LocalDeclarationStatement(
						VariableDeclaration(IdentifierName("var"))
							.WithVariables(SingletonSeparatedList(
								VariableDeclarator(Identifier(name))
									.WithInitializer(EqualsValueClause(Unparenthesize(candidate)))
							))
					);
					newStatements.Add(declaration);
				}
			}

			// Rewrite the statement using the current replacement map
			var rewriter = new ExpressionReplacementRewriter(replacementMap);
			newStatements.Add((StatementSyntax) rewriter.Visit(currentStatement));
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

	private static bool ShouldConsider(ExpressionSyntax expr, HashSet<ExpressionSyntax> lValues, HashSet<ExpressionSyntax> sideEffectCalls)
	{
		expr = Unparenthesize(expr);

		// Expressions used as L-values cannot be CSE'd safely
		if (lValues.Contains(expr))
		{
			return false;
		}

		// If any identifier referenced by the expression is also mutated in this block,
		// the expression's value may change between occurrences and cannot be safely CSE'd.
		// e.g. in `positions = positions % 6; if (...) positions += 6; result[0] = ...[positions % 6]`
		// `positions % 6` appears twice but `positions` is mutated in between.
		if (expr.DescendantNodesAndSelf()
		    .OfType<IdentifierNameSyntax>()
		    .Any(id => lValues.Any(lv => lv is IdentifierNameSyntax lId && lId.Identifier.Text == id.Identifier.Text)))
		{
			return false;
		}

		// Only consider "expensive" or complex expressions
		if (expr is BinaryExpressionSyntax)
		{
			return true;
		}

		// A repeated ternary (e.g. `Char.IsUpper(c) ? 'A' : 'a'`) is worth hoisting into a single
		// local. Exclude lambda-bearing conditionals for the same `var`-inference reason as invocations.
		if (expr is ConditionalExpressionSyntax)
		{
			return !expr.DescendantNodes().Any(n => n is LambdaExpressionSyntax or AnonymousFunctionExpressionSyntax);
		}

		if (expr is InvocationExpressionSyntax invocation)
		{
			// Calls that appear as expression statements are called for their side effects —
			// extracting them to a variable would elide the side effect on subsequent uses.
			if (sideEffectCalls.Contains(invocation))
			{
				return false;
			}

			// Avoid CSE for expressions containing lambdas, as 'var' might fail to infer the delegate type
			return !invocation.DescendantNodes().Any(n => n is LambdaExpressionSyntax or AnonymousFunctionExpressionSyntax);
		}

		if (expr is MemberAccessExpressionSyntax ma)
		{
			return ShouldConsider(ma.Expression, lValues, sideEffectCalls);
		}

		if (expr is ElementAccessExpressionSyntax)
		{
			return true;
		}

		if (expr is CastExpressionSyntax cast)
		{
			return ShouldConsider(cast.Expression, lValues, sideEffectCalls);
		}

		return false;
	}

	private static ExpressionSyntax Unparenthesize(ExpressionSyntax expr)
	{
		while (expr is ParenthesizedExpressionSyntax p)
		{
			expr = p.Expression;
		}

		return expr;
	}

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

			return SyntaxNodeComparer.Get<ExpressionSyntax>().Equals(Unparenthesize(x), Unparenthesize(y));
		}

		public int GetHashCode(ExpressionSyntax obj)
		{
			return SyntaxNodeComparer.Get<ExpressionSyntax>().GetHashCode(Unparenthesize(obj));
		}
	}

	private class ExpressionCollector(Dictionary<ExpressionSyntax, int> counts, HashSet<ExpressionSyntax> lValues, HashSet<ExpressionSyntax> sideEffectCalls) : CSharpSyntaxWalker
	{
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
			base.VisitAssignmentExpression(node);
		}

		public override void VisitPrefixUnaryExpression(PrefixUnaryExpressionSyntax node)
		{
			if (node.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PostIncrementExpression or
			    SyntaxKind.PreDecrementExpression or SyntaxKind.PostDecrementExpression)
			{
				lValues.Add(Unparenthesize(node.Operand));
			}
			base.VisitPrefixUnaryExpression(node);
		}

		public override void VisitPostfixUnaryExpression(PostfixUnaryExpressionSyntax node)
		{
			if (node.Kind() is SyntaxKind.PreIncrementExpression or SyntaxKind.PostIncrementExpression or
			    SyntaxKind.PreDecrementExpression or SyntaxKind.PostDecrementExpression)
			{
				lValues.Add(Unparenthesize(node.Operand));
			}
			base.VisitPostfixUnaryExpression(node);
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
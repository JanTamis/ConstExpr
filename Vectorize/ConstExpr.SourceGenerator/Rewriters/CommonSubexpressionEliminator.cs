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
				SyntaxKind.LessThanExpression => "lt",
				SyntaxKind.LessThanOrEqualExpression => "lte",
				SyntaxKind.GreaterThanExpression => "gt",
				SyntaxKind.GreaterThanOrEqualExpression => "gte",
				SyntaxKind.EqualsExpression => "eq",
				SyntaxKind.NotEqualsExpression => "ne",
				SyntaxKind.LogicalAndExpression => "and",
				SyntaxKind.LogicalOrExpression => "or",
				_ => "val"
			},
			InvocationExpressionSyntax invocation => invocation.Expression switch
			{
				IdentifierNameSyntax id => $"{SanitizeIdentifierPart(id.Identifier.Text)}Val",
				MemberAccessExpressionSyntax ma => $"{SanitizeIdentifierPart(ma.Expression.TryGetInferredMemberName() ?? String.Empty)}{ma.Name.Identifier.Text}",
				_ => "callVal"
			},
			MemberAccessExpressionSyntax ma => $"{ma.Expression}{ma.Name.Identifier.Text}",
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

			return end == 0 ? String.Empty : Char.ToLowerInvariant(text[0]) + text.Substring(1, end - 1);
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
		var unconditionalOccurrences = new HashSet<ExpressionSyntax>(_comparer);
		var ternaryFreeOccurrences = new HashSet<ExpressionSyntax>(_comparer);
		var collector = new ExpressionCollector(counts, lValues, sideEffectCalls, mutatedNames, unconditionalOccurrences, ternaryFreeOccurrences);

		foreach (var statement in visitedNode.Statements)
		{
			collector.Visit(statement);
		}

		// A candidate whose every occurrence sits inside a single ternary branch (e.g. `sum * sum`
		// duplicated only within `cond ? sum * sum : 0`) is only ever evaluated when that branch
		// runs. Hoisting it to a `var` declaration before the statement would evaluate it
		// unconditionally instead, which changes behavior for any expression that can throw and
		// changes performance for any expensive one. Requiring at least one occurrence outside every
		// ternary branch (i.e. already evaluated unconditionally, in the condition or at the
		// statement's top level) keeps hoisting to cases where moving the evaluation earlier is safe.
		//
		// A short-circuit (&&/||) right operand is conditional the same way, but for a DIFFERENT
		// reason than a ternary branch: it's not an alternative that might not have "won" (both a
		// ternary's branches produce a value; only one runs), it's a later term in a sequential
		// chain that just might not be reached. For an expression that can never throw or have a
		// side effect (see IsProvablyPureArithmetic), evaluating it earlier than the original chain
		// would have changes nothing observable, so a ternary-free-but-short-circuit-only occurrence
		// is just as safe to hoist as a fully unconditional one — unlike a ternary branch, where
		// forcing both arms is never a no-op even for pure arithmetic (the arm may exist specifically
		// to avoid the cost of the other).
		var allCandidates = counts.Where(kvp => kvp.Value > 1
		                                        && (unconditionalOccurrences.Contains(kvp.Key)
		                                            || ternaryFreeOccurrences.Contains(kvp.Key) && IsProvablyPureArithmetic(kvp.Key))
		                                        && ShouldConsider(kvp.Key, lValues, sideEffectCalls, mutatedNames))
			.Select(kvp => kvp.Key)
			.ToList();

		if (allCandidates.Count == 0)
		{
			return visitedNode;
		}

		var candidateKeys = new HashSet<ExpressionSyntax>(allCandidates, _comparer);

		// A candidate every one of whose occurrences sits inside another candidate's occurrence
		// (e.g. `x*y` occurring only as part of repeated `x*y+1`) has nothing to gain from its own
		// declaration: the containing candidate's hoist already covers it (see the outer-match
		// short-circuit in ExpressionReplacementRewriter.Visit below), and a separate `var prod = x*y;`
		// would just sit there unused. Drop those; keep candidates that also occur bare somewhere
		// (e.g. `x.Length` reused outside of `x.Length + 2`), so the containing candidate's
		// initializer can reference the hoisted variable instead of re-reading the raw subexpression.
		var candidates = OrderByContainment(allCandidates
			.Where(c => !IsFullyContainedInAnotherCandidate(c, visitedNode, candidateKeys))
			.ToList(), visitedNode);

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

				if (ContainsUnconditionalOccurrence(statement, candidate)
				    || IsProvablyPureArithmetic(candidate) && ContainsTernaryFreeOccurrence(statement, candidate))
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
			newStatements.Add((StatementSyntax) rewriter.Visit(statement)!);
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
					: BuildChain([ .. unique, sharedProduct ], SyntaxKind.MultiplyExpression);

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
				var rebuilt = BuildChain([ flat.Base, refTerm, .. remaining ], SyntaxKind.SubtractExpression);

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

	/// <summary>
	///   Whether <paramref name="candidate" /> occurs somewhere in <paramref name="root" /> that is
	///   guaranteed to run — mirrors <see cref="ExpressionCollector" />'s conditional-branch rules
	///   (a ternary's branches and a short-circuit <c>&amp;&amp;</c>/<c>||</c>'s right operand are
	///   NOT guaranteed). Used to pick the insertion point for a hoisted declaration: inserting
	///   before a statement whose only occurrence is conditional would evaluate the candidate
	///   somewhere the original never did — e.g. hoisting <c>s.Length</c> out of
	///   <c>IsNullOrEmpty(s) || s.Length &lt; 5</c> would throw on a null <c>s</c> where the
	///   original short-circuited before ever reading <c>Length</c>.
	/// </summary>
	private static bool ContainsUnconditionalOccurrence(SyntaxNode root, ExpressionSyntax candidate)
	{
		return ContainsOccurrence(root, candidate, true);
	}

	/// <summary>
	///   Like <see cref="ContainsUnconditionalOccurrence" />, but doesn't treat a short-circuit
	///   <c>&amp;&amp;</c>/<c>||</c>'s right operand as conditional — only ternary branches still are.
	///   Only meaningful combined with <see cref="IsProvablyPureArithmetic" />: a candidate that can
	///   never throw or have a side effect loses nothing by being evaluated earlier than a sequential
	///   <c>&amp;&amp;</c>/<c>||</c> chain would have reached it (unlike a ternary, where exactly one
	///   branch runs and forcing both is never a no-op — see <see cref="ContainsUnconditionalOccurrence" />'s
	///   own doc for why short-circuit and ternary conditionality get treated the same there).
	/// </summary>
	private static bool ContainsTernaryFreeOccurrence(SyntaxNode root, ExpressionSyntax candidate)
	{
		return ContainsOccurrence(root, candidate, false);
	}

	private static bool ContainsOccurrence(SyntaxNode root, ExpressionSyntax candidate, bool blockShortCircuit)
	{
		return Walk(root, false);

		bool Walk(SyntaxNode? node, bool conditional)
		{
			switch (node)
			{
				case null:
				case BlockSyntax:
				case AnonymousFunctionExpressionSyntax:
					return false;

				case ExpressionSyntax expr when !conditional && _comparer.Equals(Unparenthesize(expr), candidate):
					return true;

				case ConditionalExpressionSyntax cond:
					return Walk(cond.Condition, conditional) || Walk(cond.WhenTrue, true) || Walk(cond.WhenFalse, true);

				case BinaryExpressionSyntax binary when blockShortCircuit && (binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression)):
					return Walk(binary.Left, conditional) || Walk(binary.Right, true);

				default:
					foreach (var child in node.ChildNodes())
					{
						if (Walk(child, conditional))
						{
							return true;
						}
					}
					return false;
			}
		}
	}

	/// <summary>
	///   Whether <paramref name="expr" /> is built entirely from operations that can never throw or
	///   have a side effect: literals, identifiers, the non-throwing arithmetic/bitwise/comparison
	///   binary operators (division and modulo are excluded — they can throw
	///   <see cref="DivideByZeroException" /> for integer operands), explicit casts to a numeric
	///   primitive type, and numeric unary +/-/~. Deliberately excludes member/element access,
	///   invocations, and casts to anything else — those can throw or run arbitrary code, and (unlike
	///   the arithmetic here) telling pure ones from unsafe ones needs type information this rewriter
	///   doesn't have (see the trust-level note on invocation purity in <see cref="ShouldConsider" />).
	///   Used to let a short-circuit-only-conditional occurrence (see
	///   <see cref="ContainsTernaryFreeOccurrence" />) count as safe to hoist anyway.
	/// </summary>
	private static bool IsProvablyPureArithmetic(ExpressionSyntax expr)
	{
		expr = Unparenthesize(expr);

		switch (expr)
		{
			case LiteralExpressionSyntax:
			case IdentifierNameSyntax:
				return true;

			case BinaryExpressionSyntax binary when IsPureArithmeticOperator(binary.Kind()):
				return IsProvablyPureArithmetic(binary.Left) && IsProvablyPureArithmetic(binary.Right);

			case CastExpressionSyntax cast when cast.Type is PredefinedTypeSyntax predefined && IsNumericKeyword(predefined.Keyword.Kind()):
				return IsProvablyPureArithmetic(cast.Expression);

			case PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.UnaryMinusExpression) || prefix.IsKind(SyntaxKind.UnaryPlusExpression) || prefix.IsKind(SyntaxKind.BitwiseNotExpression):
				return IsProvablyPureArithmetic(prefix.Operand);

			default:
				return false;
		}
	}

	private static bool IsPureArithmeticOperator(SyntaxKind kind)
	{
		return kind switch
		{
			SyntaxKind.AddExpression or SyntaxKind.SubtractExpression or SyntaxKind.MultiplyExpression
				or SyntaxKind.LeftShiftExpression or SyntaxKind.RightShiftExpression
				or SyntaxKind.BitwiseAndExpression or SyntaxKind.BitwiseOrExpression or SyntaxKind.ExclusiveOrExpression
				or SyntaxKind.LessThanExpression or SyntaxKind.LessThanOrEqualExpression
				or SyntaxKind.GreaterThanExpression or SyntaxKind.GreaterThanOrEqualExpression
				or SyntaxKind.EqualsExpression or SyntaxKind.NotEqualsExpression => true,
			_ => false
		};
	}

	private static bool IsNumericKeyword(SyntaxKind kind)
	{
		return kind switch
		{
			SyntaxKind.IntKeyword or SyntaxKind.LongKeyword or SyntaxKind.ShortKeyword or SyntaxKind.ByteKeyword
				or SyntaxKind.UIntKeyword or SyntaxKind.ULongKeyword or SyntaxKind.UShortKeyword or SyntaxKind.SByteKeyword
				or SyntaxKind.FloatKeyword or SyntaxKind.DoubleKeyword or SyntaxKind.DecimalKeyword or SyntaxKind.CharKeyword => true,
			_ => false
		};
	}

	/// <summary>
	///   Whether every occurrence of <paramref name="candidate" /> in <paramref name="block" /> sits
	///   inside an occurrence of some other candidate in <paramref name="candidateKeys" />. Such a
	///   candidate gains nothing from its own declaration: whichever containing candidate gets hoisted
	///   matches the whole enclosing expression first and (per <see cref="ExpressionReplacementRewriter" />'s
	///   outer-match-wins, no-recurse rule) never even looks at the nested occurrence, so a separate
	///   declaration for it would just be dead code. Kept candidates are exactly the ones with at least
	///   one occurrence that stands on its own — those need their own local so a containing candidate's
	///   initializer can reference it instead of re-evaluating the raw subexpression.
	/// </summary>
	private static bool IsFullyContainedInAnotherCandidate(ExpressionSyntax candidate, BlockSyntax block, HashSet<ExpressionSyntax> candidateKeys)
	{
		return GetOccurrences(candidate, block)
			.All(occurrence => GetScopedAncestors(occurrence).Any(a => candidateKeys.Contains(a)));
	}

	/// <summary>
	///   Every occurrence of <paramref name="expr" /> in <paramref name="block" />, matched the same
	///   way <see cref="ExpressionCollector" /> counts them (structural equality via <see cref="_comparer" />).
	/// </summary>
	private static IEnumerable<ExpressionSyntax> GetOccurrences(ExpressionSyntax expr, BlockSyntax block)
	{
		return block.Statements
			.SelectMany(statement => statement.DescendantNodesAndSelf(n => n is not BlockSyntax && n is not AnonymousFunctionExpressionSyntax))
			.OfType<ExpressionSyntax>()
			.Where(e => _comparer.Equals(Unparenthesize(e), expr));
	}

	/// <summary>
	///   Ancestor expressions of <paramref name="node" /> up to (not including) the nearest enclosing
	///   block/lambda — the same scoping boundary <see cref="ExpressionCollector" /> and <see cref="GetOccurrences" />
	///   use, so an ancestor found here is guaranteed to be a candidate the collector could also have counted.
	/// </summary>
	private static IEnumerable<ExpressionSyntax> GetScopedAncestors(SyntaxNode node)
	{
		return node.Ancestors()
			.TakeWhile(a => a is not BlockSyntax && a is not AnonymousFunctionExpressionSyntax)
			.OfType<ExpressionSyntax>();
	}

	/// <summary>
	///   Orders surviving candidates so that whenever one candidate (e.g. <c>x.Length</c>) occurs
	///   nested inside another (e.g. <c>x.Length + 2</c>), the nested one comes first — otherwise the
	///   containing candidate's own initializer is built before the nested one has a replacement name
	///   to substitute, and it re-reads the raw subexpression instead of reusing the hoisted local
	///   (see the initializer-substitution step below). Candidates with no such relationship (e.g. two
	///   unrelated calls) keep their original source order — sorting those by size as well would
	///   reorder independent declarations for no reason, contrary to the source's evaluation order.
	/// </summary>
	private static List<ExpressionSyntax> OrderByContainment(List<ExpressionSyntax> candidates, BlockSyntax block)
	{
		var mustPrecede = candidates.ToDictionary(c => c, _ => new List<ExpressionSyntax>());
		var unsatisfiedPredecessors = candidates.ToDictionary(c => c, _ => 0);

		foreach (var inner in candidates)
		{
			foreach (var outer in candidates)
			{
				if (ReferenceEquals(inner, outer))
				{
					continue;
				}

				if (GetOccurrences(inner, block).Any(occ => GetScopedAncestors(occ).Any(a => _comparer.Equals(a, outer))))
				{
					mustPrecede[inner].Add(outer);
					unsatisfiedPredecessors[outer]++;
				}
			}
		}

		var remaining = new List<ExpressionSyntax>(candidates);
		var ordered = new List<ExpressionSyntax>();

		while (remaining.Count > 0)
		{
			// Among candidates with no unsatisfied predecessor (i.e. not required by a containment
			// edge to come later), prefer the larger expression, same as this pass always has for
			// unrelated candidates — nesting edges are what actually need ordering; this only breaks
			// ties between candidates that don't nest inside one another at all.
			var next = remaining
				.Where(c => unsatisfiedPredecessors[c] == 0)
				.OrderByDescending(c => c.DescendantNodes().Count())
				.First();

			ordered.Add(next);
			remaining.Remove(next);

			foreach (var successor in mustPrecede[next])
			{
				unsatisfiedPredecessors[successor]--;
			}
		}

		return ordered;
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
			// Avoid CSE for expressions containing lambdas, as 'var' might fail to infer the delegate type.
			// (No purity check on the callee: by the time CSE runs, earlier passes have already rebuilt
			// the tree, so nodes here are no longer part of the tree any SemanticModel was built for —
			// a live symbol lookup never resolves. Same trust level this codebase already extends to
			// invocations; a real purity gate would need type info threaded through some other way.)
			InvocationExpressionSyntax invocation => !invocation.DescendantNodes().Any(n => n is LambdaExpressionSyntax or AnonymousFunctionExpressionSyntax),
			// `Unsafe.BitCast<bool, byte>` as the callee of `Unsafe.BitCast<bool, byte>(x)` is a method
			// group, not a value — it has no runtime representation `var` can bind to (`var f =
			// Unsafe.BitCast<bool, byte>;` doesn't compile). Two invocations of the same generic method
			// with different arguments still share this identical callee sub-expression, so without this
			// guard it looks like an ordinary repeated member access and gets hoisted into nonsense.
			MemberAccessExpressionSyntax ma when ma.Parent is InvocationExpressionSyntax invocation && invocation.Expression == ma => false,
			// A property getter is as legitimate a candidate as a method call — same reasoning, same
			// trust level — and the receiver itself must be a safe shape to re-read.
			MemberAccessExpressionSyntax ma => IsSafeReceiver(ma.Expression, lValues, sideEffectCalls, mutatedNames),
			// Array indexing (and a custom indexer, at the same trust level as any other member access).
			ElementAccessExpressionSyntax ea => IsSafeReceiver(ea.Expression, lValues, sideEffectCalls, mutatedNames),
			CastExpressionSyntax cast => ShouldConsider(cast.Expression, lValues, sideEffectCalls, mutatedNames),
			_ => false
		};
	}

	/// <summary>
	///   Whether an expression used as a member/element-access receiver is a safe shape to read twice.
	///   A bare identifier is a local/parameter (mutation of those is already excluded above) or an
	///   implicit-<c>this</c> member/type reference — either way there's nothing here that isn't
	///   already covered by the mutation check. Anything else falls back to the general candidate
	///   check (nested member/element access, invocation, cast, …).
	/// </summary>
	private static bool IsSafeReceiver(ExpressionSyntax expr, HashSet<ExpressionSyntax> lValues, HashSet<ExpressionSyntax> sideEffectCalls, HashSet<string> mutatedNames)
	{
		expr = Unparenthesize(expr);

		if (expr is IdentifierNameSyntax or ThisExpressionSyntax or BaseExpressionSyntax)
		{
			return true;
		}

		return ShouldConsider(expr, lValues, sideEffectCalls, mutatedNames);
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
		public override SyntaxNode VisitBinaryExpression(BinaryExpressionSyntax node)
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

	private class ExpressionCollector(Dictionary<ExpressionSyntax, int> counts, HashSet<ExpressionSyntax> lValues, HashSet<ExpressionSyntax> sideEffectCalls, HashSet<string> mutatedNames, HashSet<ExpressionSyntax> unconditionalOccurrences, HashSet<ExpressionSyntax> ternaryFreeOccurrences) : CSharpSyntaxWalker
	{
		// Tracked separately from _shortCircuitDepth: a ternary branch and a short-circuit right
		// operand are both "conditional", but for different reasons (see IsProvablyPureArithmetic /
		// ContainsTernaryFreeOccurrence) — only ternary depth blocks the pure-arithmetic escape hatch.
		private int _ternaryDepth;
		private int _shortCircuitDepth;

		private void MarkMutated(ExpressionSyntax target)
		{
			if (GetBaseIdentifier(target) is { } name)
			{
				mutatedNames.Add(name);
			}
		}

		// A ternary's condition is always evaluated, but only one of its two branches runs, so
		// occurrences inside WhenTrue/WhenFalse must not count as "unconditional" the way a plain
		// top-level occurrence does.
		public override void VisitConditionalExpression(ConditionalExpressionSyntax node)
		{
			Visit(node.Condition);

			_ternaryDepth++;
			Visit(node.WhenTrue);
			Visit(node.WhenFalse);
			_ternaryDepth--;
		}

		// The right operand of `&&`/`||` only runs if the left operand doesn't already decide the
		// result (short-circuit evaluation) — same "not guaranteed to run" hazard as a ternary
		// branch, e.g. the `s.Length` in `IsNullOrEmpty(s) || s.Length < 5` never executes when `s`
		// is null.
		public override void VisitBinaryExpression(BinaryExpressionSyntax node)
		{
			if (!node.IsKind(SyntaxKind.LogicalAndExpression) && !node.IsKind(SyntaxKind.LogicalOrExpression))
			{
				base.VisitBinaryExpression(node);
				return;
			}

			Visit(node.Left);

			_shortCircuitDepth++;
			Visit(node.Right);
			_shortCircuitDepth--;
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

				if (_ternaryDepth == 0 && _shortCircuitDepth == 0)
				{
					unconditionalOccurrences.Add(normalized);
				}

				if (_ternaryDepth == 0)
				{
					ternaryFreeOccurrences.Add(normalized);
				}
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
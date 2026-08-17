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
public sealed class CommonSubexpressionEliminator(bool allowReassociation = false, bool allowReciprocal = false) : CSharpSyntaxRewriter
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
				MemberAccessExpressionSyntax { Name.Identifier.Text: "ReciprocalEstimate" or "ReciprocalSqrtEstimate" }
					when invocation.ArgumentList.Arguments is [ { Expression: IdentifierNameSyntax argId } ]
					=> $"inv{CapitalizeIdentifierPart(argId.Identifier.Text)}",
				MemberAccessExpressionSyntax ma => $"{SanitizeIdentifierPart(GetHostNameHint(ma.Expression))}{ma.Name.Identifier.Text}",
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

		string CapitalizeIdentifierPart(string text)
		{
			var sanitized = SanitizeIdentifierPart(text);

			return sanitized.Length == 0 ? sanitized : Char.ToUpperInvariant(sanitized[0]) + sanitized.Substring(1);
		}

		while (_usedNames.Contains(name))
		{
			name = $"{baseName}{++counter}";
		}

		_usedNames.Add(name);
		return name;
	}

	private static string GetHostNameHint(ExpressionSyntax host)
	{
		return host is PredefinedTypeSyntax predefined
			? predefined.Keyword.Text
			: host.TryGetInferredMemberName() ?? String.Empty;
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

		var eliminator = new CommonSubexpressionEliminator(
			mathOptimizations.HasFlag(FastMathFlags.AssociativeMath),
			mathOptimizations.HasFlag(FastMathFlags.ReciprocalMath));
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

		if (allowReassociation || allowReciprocal)
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
		//
		// That last point is about a candidate in ONE arm. A candidate in EVERY arm of an exhaustive
		// alternative is a third, separate case: it already ran whichever way the branch went, so
		// hoisting forces nothing and is free even when it can throw. That's partial redundancy, and
		// IsPartiallyRedundantIn decides it — anchored to a single statement, so this filter and the
		// insertion-point search below can never disagree about a candidate.
		var allCandidates = counts.Where(kvp => kvp.Value > 1
		                                        && (unconditionalOccurrences.Contains(kvp.Key)
		                                            || ternaryFreeOccurrences.Contains(kvp.Key) && IsProvablyPureArithmetic(kvp.Key)
		                                            || visitedNode.Statements.Any(s => IsPartiallyRedundantIn(s, kvp.Key)))
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
				    || IsProvablyPureArithmetic(candidate) && ContainsTernaryFreeOccurrence(statement, candidate)
				    || IsPartiallyRedundantIn(statement, candidate))
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
	private BlockSyntax CanonicalizeForCse(BlockSyntax block)
	{
		if (allowReassociation)
		{
			block = CanonicalizeMultiplicationFactors(block);
			block = CanonicalizeSubtractionPrefixes(block);
		}

		if (allowReciprocal)
		{
			block = CanonicalizeReciprocalDivision(block);
		}

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
	///   Finds a symbolic (non-literal) denominator divided into by two or more expressions, at
	///   least two of which are guaranteed to run together (see <see cref="ContainsUnconditionalOccurrence" />
	///   — a denominator divided into only across mutually-exclusive branches has nothing to share),
	///   and rewrites every one of those divisions from <c>x / d</c> to <c>x * Receiver.ReciprocalEstimate(d)</c>,
	///   letting the ordinary exact-match CSE pass that follows hoist the now-repeated
	///   <c>ReciprocalEstimate</c> call into a single shared local. This never widens which
	///   occurrences get evaluated — it only reshapes each division in place — so the safety of the
	///   actual hoist is left entirely to CSE's own candidate rules.
	///   <para>
	///     The receiver for <c>ReciprocalEstimate</c> (e.g. <c>Double</c>) is not resolved through the
	///     semantic model — a denominator this pass rewrites can be a synthetic local a fast-math pass
	///     introduced earlier in the pipeline, which the model was never built to answer for. Instead
	///     it's read straight off the denominator's own declaration when that declaration is itself a
	///     <c>Receiver.MaxNative(...)</c>/<c>Receiver.MinNative(...)</c>-shaped reduction (the
	///     convention <see cref="ConstExpr.SourceGenerator.Optimizers.FunctionOptimizers.MathOptimizers.MaxFunctionOptimizer" />
	///     and <see cref="MaxMinScaleFactorRewriter" /> both already use only for numeric helper types
	///     that expose it) — declining rather than guessing when the denominator has any other shape.
	///   </para>
	/// </summary>
	private static BlockSyntax CanonicalizeReciprocalDivision(BlockSyntax block)
	{
		var divisionsByDenominator = block.DescendantNodes()
			.OfType<BinaryExpressionSyntax>()
			.Where(b => b.IsKind(SyntaxKind.DivideExpression) && Unparenthesize(b.Right) is IdentifierNameSyntax)
			.GroupBy(b => ((IdentifierNameSyntax) Unparenthesize(b.Right)).Identifier.Text)
			.Where(g => g.Count() >= 2);

		var replacements = new Dictionary<ExpressionSyntax, ExpressionSyntax>();

		foreach (var group in divisionsByDenominator)
		{
			var divisions = group.ToList();

			// ContainsUnconditionalOccurrence expects a single statement as its root (a BlockSyntax
			// root always answers false, by design — see its doc), so each division is checked
			// against its own enclosing top-level statement, not the whole block.
			var unconditionalCount = divisions.Count(d =>
				block.Statements.FirstOrDefault(s => s.Contains(d)) is { } enclosing && ContainsUnconditionalOccurrence(enclosing, d));

			if (unconditionalCount < 2)
			{
				continue;
			}

			if (!TryFindReciprocalReceiver(block, group.Key, out var receiver))
			{
				continue;
			}

			foreach (var division in divisions)
			{
				var reciprocalCall = InvocationExpression(
					MemberAccessExpression(SyntaxKind.SimpleMemberAccessExpression, receiver, IdentifierName("ReciprocalEstimate")),
					ArgumentList(SingletonSeparatedList(Argument(Unparenthesize(division.Right)))));

				replacements[division] = MultiplyExpression(division.Left, reciprocalCall);
			}
		}

		return replacements.Count == 0 ? block : block.ReplaceNodes(replacements.Keys, (orig, _) => replacements[orig]);
	}

	/// <summary>
	///   A denominator's own declaration reveals its numeric helper type only when that declaration
	///   is itself a <c>Receiver.Max/MaxNative/Min/MinNative(...)</c> reduction — reused verbatim so
	///   the emitted <c>Receiver.ReciprocalEstimate(...)</c> matches whatever spelling (<c>double</c>
	///   vs. <c>Double</c>) that reduction already used.
	/// </summary>
	private static bool TryFindReciprocalReceiver(BlockSyntax block, string denominatorName, out ExpressionSyntax receiver)
	{
		receiver = null!;

		var declarator = block.Statements
			.OfType<LocalDeclarationStatementSyntax>()
			.Select(s => s.Declaration.Variables.FirstOrDefault(v => v.Identifier.Text == denominatorName))
			.FirstOrDefault(v => v is not null);

		if (declarator?.Initializer?.Value is not InvocationExpressionSyntax
		    {
			    Expression: MemberAccessExpressionSyntax { Name.Identifier.Text: "Max" or "MaxNative" or "Min" or "MinNative" } member
		    })
		{
			return false;
		}

		receiver = member.Expression;
		return true;
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
				{
					return false;
				}

				case ExpressionSyntax expr when !conditional && _comparer.Equals(Unparenthesize(expr), candidate):
				{
					return true;
				}

				case ConditionalExpressionSyntax cond:
				{
					return Walk(cond.Condition, conditional) || Walk(cond.WhenTrue, true) || Walk(cond.WhenFalse, true);
				}

				case BinaryExpressionSyntax binary when blockShortCircuit && (binary.IsKind(SyntaxKind.LogicalAndExpression) || binary.IsKind(SyntaxKind.LogicalOrExpression)):
				{
					return Walk(binary.Left, conditional) || Walk(binary.Right, true);
				}

				default:
				{
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
	}

	/// <summary>
	///   Whether <paramref name="candidate" /> is worth hoisting in front of <paramref name="statement" />
	///   under the partial-redundancy rule: evaluated on every path through it (see
	///   <see cref="IsEvaluatedOnEveryPath" />) and not invalidated by a mutation hiding in a nested
	///   block (see <see cref="IsMutatedAnywhereWithin" />).
	///   <para>
	///     Both the candidate filter and the insertion-point search go through this one helper, and both
	///     ask it about a <em>single statement</em>. They have to agree: a candidate selected because
	///     some statement satisfies this must be one the insertion-point loop will also accept, or it
	///     gets picked, never gets a declaration, and silently goes unreplaced. Asking the filter about
	///     the whole block instead would break exactly that — a candidate could qualify on occurrences
	///     spread across two different <c>if</c> statements while no single statement qualifies.
	///   </para>
	/// </summary>
	private static bool IsPartiallyRedundantIn(SyntaxNode statement, ExpressionSyntax candidate)
	{
		return IsEvaluatedOnEveryPath(statement, candidate) && !IsMutatedAnywhereWithin(statement, candidate);
	}

	/// <summary>
	///   Whether <paramref name="candidate" /> is evaluated on every path through <paramref name="node" />.
	///   Unlike <see cref="ContainsUnconditionalOccurrence" />, which needs an occurrence that is
	///   syntactically unconditional, this also accepts one that appears in <em>every</em> arm of an
	///   exhaustive alternative — both arms of a ternary, or an <c>if</c>/<c>else</c> with both branches
	///   present. Exactly one arm ever runs and all of them evaluate the candidate, so it is evaluated
	///   exactly once either way: hoisting it in front of the construct changes neither the number of
	///   evaluations nor which exceptions can escape, which makes it free even for an expression that
	///   can throw.
	///   <para>
	///     Note this does not contradict the cost argument against forcing a ternary arm (see
	///     <see cref="VisitBlock" />'s candidate filter): that is about a candidate in <em>one</em> arm,
	///     where hoisting adds an evaluation the other arm deliberately avoided. In every arm, nothing
	///     is forced — it already ran whichever way the branch went.
	///   </para>
	///   <para>
	///     This is why it descends into <see cref="BlockSyntax" /> where <see cref="ContainsOccurrence" />
	///     deliberately stops: a branch's occurrences live behind a block boundary, so refusing to look
	///     there would make the whole rule dead. That widening is also what makes
	///     <see cref="IsMutatedAnywhereWithin" /> mandatory — see its own doc.
	///   </para>
	/// </summary>
	private static bool IsEvaluatedOnEveryPath(SyntaxNode? node, ExpressionSyntax candidate)
	{
		switch (node)
		{
			case null:
			{
				return false;
			}

			case ExpressionSyntax expr when _comparer.Equals(Unparenthesize(expr), candidate):
			{
				return true;
			}

			// Exactly one arm runs, so the candidate is guaranteed if the always-evaluated condition
			// has it — or if EVERY arm does.
			case ConditionalExpressionSyntax cond:
			{
				return IsEvaluatedOnEveryPath(cond.Condition, candidate)
				       || IsEvaluatedOnEveryPath(cond.WhenTrue, candidate) && IsEvaluatedOnEveryPath(cond.WhenFalse, candidate);
			}

			// The same shape as the ternary above, one level up. Without an `else` the construct is not
			// exhaustive: the fall-through path evaluates nothing, so a then-only occurrence proves nothing.
			case IfStatementSyntax ifStatement:
			{
				return IsEvaluatedOnEveryPath(ifStatement.Condition, candidate)
				       || ifStatement.Else is { } elseClause
				       && IsEvaluatedOnEveryPath(ifStatement.Statement, candidate)
				       && IsEvaluatedOnEveryPath(elseClause.Statement, candidate);
			}

			// Only the left operand is guaranteed; the right can be short-circuited away entirely.
			case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.LogicalAndExpression)
			                                        || binary.IsKind(SyntaxKind.LogicalOrExpression)
			                                        || binary.IsKind(SyntaxKind.CoalesceExpression):
			{
				return IsEvaluatedOnEveryPath(binary.Left, candidate);
			}

			// `a?.b` evaluates `a`, but reaches `.b` only when `a` is non-null.
			case ConditionalAccessExpressionSyntax conditionalAccess:
			{
				return IsEvaluatedOnEveryPath(conditionalAccess.Expression, candidate);
			}

			case BlockSyntax block:
			{
				return IsEvaluatedOnEveryStatementPath(block, candidate);
			}

			case not null when IsNeverGuaranteedToRun(node):
			{
				return false;
			}

			default:
			{
				foreach (var child in node.ChildNodes())
				{
					if (IsEvaluatedOnEveryPath(child, candidate))
					{
						return true;
					}
				}

				return false;
			}
		}
	}

	/// <summary>
	///   Whether <paramref name="candidate" /> is evaluated on every path through
	///   <paramref name="block" />. Statements run in order, but an early exit before an occurrence
	///   creates a path that skips it — in <c>if (d) return 0; return a.Length;</c> the read is NOT on
	///   every path, so hoisting it in front of the block would evaluate it where the original never
	///   did (and throw where the original returned). Hence: walk forwards, and stop at the first
	///   statement that can transfer control out.
	/// </summary>
	private static bool IsEvaluatedOnEveryStatementPath(BlockSyntax block, ExpressionSyntax candidate)
	{
		foreach (var statement in block.Statements)
		{
			if (IsEvaluatedOnEveryPath(statement, candidate))
			{
				return true;
			}

			if (CanExitEarly(statement))
			{
				return false;
			}
		}

		return false;
	}

	/// <summary>
	///   Constructs nothing inside of which is guaranteed to run: a loop body may iterate zero times, a
	///   lambda or local function may never be invoked, a <c>switch</c>'s exhaustiveness can't be proven
	///   without the type information this rewriter no longer has, and a <c>try</c> body can be
	///   abandoned part-way. None of these ever qualify under the every-path rule — the ordinary
	///   unconditional rule still covers whatever it covered before.
	/// </summary>
	private static bool IsNeverGuaranteedToRun(SyntaxNode node)
	{
		return node is ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax
			or DoStatementSyntax or SwitchStatementSyntax or SwitchExpressionSyntax
			or TryStatementSyntax or AnonymousFunctionExpressionSyntax or LocalFunctionStatementSyntax;
	}

	/// <summary>
	///   Whether <paramref name="statement" /> can transfer control out of the block it sits in, which
	///   would let a later statement be skipped. Conservative: any jump anywhere in the subtree counts,
	///   without checking whether it is actually reachable.
	/// </summary>
	private static bool CanExitEarly(StatementSyntax statement)
	{
		return statement.DescendantNodesAndSelf()
			.Any(n => n is ReturnStatementSyntax or ThrowStatementSyntax or BreakStatementSyntax
				or ContinueStatementSyntax or GotoStatementSyntax or ThrowExpressionSyntax
				or YieldStatementSyntax);
	}

	/// <summary>
	///   Whether any base identifier <paramref name="candidate" /> reads is mutated anywhere inside
	///   <paramref name="statement" />, nested blocks included.
	///   <para>
	///     Needed only for candidates accepted by <see cref="IsEvaluatedOnEveryPath" />. The ordinary
	///     <c>mutatedNames</c> guard in <see cref="ShouldConsider" /> is filled by
	///     <see cref="ExpressionCollector" />, whose <c>VisitBlock</c> stops at nested blocks — so a
	///     mutation inside a loop body inside an if-branch never reaches it:
	///     <code>
	///     if (c) { foreach (var x in xs) { i++; } use(arr[i]); }
	///     else   { use(arr[i]); }
	///     </code>
	///     Both branches read <c>arr[i]</c>, but not the same <c>arr[i]</c>. Before the every-path rule
	///     such a candidate had no unconditional occurrence and was unreachable, so the gap never
	///     mattered; now it is reachable, so re-check the whole subtree with block boundaries ignored.
	///   </para>
	///   <para>
	///     An arm that merely rebinds the base at statement level (<c>a = GetArray();</c>) is already
	///     caught by <c>mutatedNames</c>: <see cref="ExpressionCollector.MarkMutated" /> works on the
	///     syntactic base identifier with no symbol distinction, so a parameter counts the same as a
	///     local, and branch statements have been visited since if-condition/branch hoisting landed.
	///   </para>
	/// </summary>
	private static bool IsMutatedAnywhereWithin(SyntaxNode statement, ExpressionSyntax candidate)
	{
		var mutated = new HashSet<string>();

		// Deliberately the same four mutation channels ExpressionCollector.MarkMutated covers — plain
		// assignment, inc/dec, indexer write (via the target's base identifier) and `ref`/`out` args —
		// just without the block scoping. A second, divergent definition of "mutated" is how these two
		// guards would drift apart.
		foreach (var node in statement.DescendantNodesAndSelf())
		{
			switch (node)
			{
				case AssignmentExpressionSyntax assignment:
				{
					Mark(assignment.Left);
					break;
				}

				case PrefixUnaryExpressionSyntax prefix when IsIncrementOrDecrement(prefix.Kind()):
				{
					Mark(prefix.Operand);
					break;
				}

				case PostfixUnaryExpressionSyntax postfix when IsIncrementOrDecrement(postfix.Kind()):
				{
					Mark(postfix.Operand);
					break;
				}

				case ArgumentSyntax argument when argument.RefKindKeyword.IsKind(SyntaxKind.RefKeyword)
				                                  || argument.RefKindKeyword.IsKind(SyntaxKind.OutKeyword):
				{
					Mark(argument.Expression);
					break;
				}
			}
		}

		return mutated.Count > 0
		       && Unparenthesize(candidate)
			       .DescendantNodesAndSelf()
			       .OfType<IdentifierNameSyntax>()
			       .Any(id => mutated.Contains(id.Identifier.Text));

		void Mark(ExpressionSyntax target)
		{
			if (GetBaseIdentifier(Unparenthesize(target)) is { } name)
			{
				mutated.Add(name);
			}
		}
	}

	private static bool IsIncrementOrDecrement(SyntaxKind kind)
	{
		return kind is SyntaxKind.PreIncrementExpression or SyntaxKind.PostIncrementExpression
			or SyntaxKind.PreDecrementExpression or SyntaxKind.PostDecrementExpression;
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
			{
				return true;
			}

			case BinaryExpressionSyntax binary when IsPureArithmeticOperator(binary.Kind()):
			{
				return IsProvablyPureArithmetic(binary.Left) && IsProvablyPureArithmetic(binary.Right);
			}

			case CastExpressionSyntax cast when cast.Type is PredefinedTypeSyntax predefined && IsNumericKeyword(predefined.Keyword.Kind()):
			{
				return IsProvablyPureArithmetic(cast.Expression);
			}

			case PrefixUnaryExpressionSyntax prefix when prefix.IsKind(SyntaxKind.UnaryMinusExpression) || prefix.IsKind(SyntaxKind.UnaryPlusExpression) || prefix.IsKind(SyntaxKind.BitwiseNotExpression):
			{
				return IsProvablyPureArithmetic(prefix.Operand);
			}

			default:
			{
				return false;
			}
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
		var occurrences = GetOccurrences(candidate, block).ToList();

		// No occurrence visible at this scope means nothing to be contained by, so the candidate must be
		// kept — `All` over an empty sequence is vacuously true and would silently drop it instead. That
		// is reachable only via the partial-redundancy rule: GetOccurrences stops at nested blocks, so a
		// candidate that lives purely inside two if/else branch blocks has none here, while every
		// candidate the older rules admit has at least one (an if-condition occurrence, say, sits outside
		// any block).
		return occurrences.Count > 0
		       && occurrences.All(occurrence => GetScopedAncestors(occurrence).Any(a => candidateKeys.Contains(a)));
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
				case ParenthesizedExpressionSyntax p:
				{
					expr = p.Expression;
					break;
				}
				case ElementAccessExpressionSyntax e:
				{
					expr = e.Expression;
					break;
				}
				case MemberAccessExpressionSyntax m:
				{
					expr = m.Expression;
					break;
				}
				case IdentifierNameSyntax id:
				{
					return id.Identifier.Text;
				}
				default:
				{
					return null;
				}
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

		// An if/else branch runs exactly when its own Condition (just visited, at the current depth)
		// implies it, and only one of Statement/Else ever runs -- the same "exactly one of several
		// alternatives" hazard a ternary's branches have (see VisitConditionalExpression above). So,
		// unlike VisitBlock's blanket stop (used for loop/lambda bodies, where a repeat can't be tied
		// to anything unconditional the way an if-condition can), branch content is still visited here
		// -- just always under the same _ternaryDepth bump a ternary branch gets, so an occurrence
		// found here can only ever ride along on a candidate that's already unconditional elsewhere
		// (typically the Condition itself), never originate one on its own. This is what lets
		// `if (numbers.Length < 2) { return numbers.Length == 1 ? ... : ...; }` hoist `numbers.Length`:
		// the Condition's occurrence carries the candidate, and the raw text still inside the branch
		// is reached and replaced by ExpressionReplacementRewriter's ordinary (block-unaware) walk.
		public override void VisitIfStatement(IfStatementSyntax node)
		{
			Visit(node.Condition);
			VisitBranch(node.Statement);

			if (node.Else is { } elseClause)
			{
				VisitBranch(elseClause.Statement);
			}
		}

		private void VisitBranch(StatementSyntax statement)
		{
			_ternaryDepth++;

			if (statement is BlockSyntax block)
			{
				foreach (var inner in block.Statements)
				{
					Visit(inner);
				}
			}
			else
			{
				Visit(statement);
			}

			_ternaryDepth--;
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
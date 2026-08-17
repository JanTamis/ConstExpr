using System.Collections.Concurrent;
using System.Collections.Generic;
using ConstExpr.Core.Attributes;
using ConstExpr.Core.Enumerators;
using ConstExpr.SourceGenerator.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConstExpr.SourceGenerator.Rewriters;

/// <summary>
///   The post-evaluation optimization passes, in the one order they are meant to run, so every body
///   that reaches the output gets the same treatment.
///   <para>
///     This used to be spelled out three times — in <see cref="ConstExprSourceGenerator" />, in the
///     test harness, and once more for the private helpers a method inlines. The three drifted: the
///     helper copy only ran CSE and stackalloc conversion, so a method emitted next to its caller
///     (<c>FindMax</c> alongside <c>Range</c>) silently missed every other pass. Adding a pass here
///     now reaches all three.
///   </para>
///   <para>
///     Still three call sites, but one of them covers a fourth kind of body: the helper the LINQ
///     unroller synthesizes for an unrolled chain reaches the inlined-body call site through
///     <c>TryUnrollLinqChain</c>'s optimize callback. It used to be emitted exactly as the unrollers
///     assembled it, which is how <c>Average(x =&gt; (x - mean) * (x - mean))</c> kept a duplicate
///     subexpression no pass had ever looked at.
///   </para>
/// </summary>
public static class OptimizationPipeline
{
	/// <summary>
	///   Runs the passes enabled by <paramref name="attribute" /> over <paramref name="body" />.
	///   <paramref name="parameters" /> and <paramref name="methodName" /> describe the enclosing
	///   method — tail-recursion elimination needs the name to spot self-calls, and bounds-check
	///   elimination needs the declared parameter types, which the semantic model can no longer supply
	///   for a rewritten tree; for a <c>var</c> local it falls back to <paramref name="variables" />
	///   instead, which still carries the type the interpreter resolved before the tree was rewritten.
	/// </summary>
	public static SyntaxNode Apply(SyntaxNode body, ParameterListSyntax parameters, SyntaxToken methodName,
	                               ConstExprAttribute attribute, IDictionary<string, VariableItem> variables, SemanticModel semanticModel,
	                               ConcurrentDictionary<ulong, ISymbol> symbolStore, IDictionary<SyntaxNode, bool> additionalMethods, ISet<string> usings)
	{
		if (attribute.Optimizations.HasFlag(OptimizationFlags.CopyPropagation))
		{
			body = Prune(CopyPropagationRewriter.Apply(body));
		}

		// After copy propagation, so a chain built from a copied local reassociates under its one
		// canonical name. Introduces no dead code (it only reshapes expressions), so no prune.
		if (attribute.Optimizations.HasFlag(OptimizationFlags.Reassociation))
		{
			body = ReassociationRewriter.Apply(body, semanticModel, symbolStore);
		}

		// Before CSE: collapsing a settled branch lifts its statements into the enclosing block, which
		// is the only scope the eliminator hoists within. After copy propagation, so a comparison on a
		// copied local is analysed under the one canonical name.
		if (attribute.Optimizations.HasFlag(OptimizationFlags.ValueRangePropagation))
		{
			body = Prune(ValueRangeRewriter.Apply(body));
		}

		// Before CSE, same reasoning as ValueRangePropagation above: folding the if/else down to its
		// surviving branch lifts that branch's statements into the enclosing block.
		if (attribute.Optimizations.HasFlag(OptimizationFlags.DefaultBranchHoisting))
		{
			body = Prune(DefaultBranchHoistingRewriter.Apply(body, variables));
		}

		// Before LoopUnswitching: unswitching moves a duplicated loop into a single-statement block per
		// arm, stranding the counter's declaration outside the block this pass inspects. Converting
		// first keeps the counter's initial value visible, and LoopUnswitchingRewriter already treats a
		// do-while exactly like a while (it overrides VisitDoStatement too), so nothing downstream loses
		// coverage.
		if (attribute.Optimizations.HasFlag(OptimizationFlags.WhileToDoWhileConversion))
		{
			body = WhileToDoWhileRewriter.Apply(body); // no Prune: converting while->do-while creates no dead code
		}

		// Before CSE, so CSE's own canonicalization and hoisting see the block with any scaled
		// Max/Min reduction and complement-ratio division already rewritten to their raw-domain
		// form — in particular the reciprocal-hoistable `Double.ReciprocalEstimate` calls this
		// pass's division cancellation can leave duplicated across a tuple/return statement.
		if (attribute.Optimizations.HasFlag(OptimizationFlags.ScaleFactorDistribution))
		{
			body = Prune(MaxMinScaleFactorRewriter.Apply(body, attribute.MathOptimizations));
		}

		if (attribute.Optimizations.HasFlag(OptimizationFlags.CommonSubexpressionElimination))
		{
			body = Prune(CommonSubexpressionEliminator.Eliminate(body, attribute.MathOptimizations) ?? body);
		}

		if (attribute.Optimizations.HasFlag(OptimizationFlags.LoopInvariantCodeMotion))
		{
			body = Prune(LoopInvariantCodeMotionRewriter.Apply(body));
		}

		if (attribute.Optimizations.HasFlag(OptimizationFlags.LoopUnswitching))
		{
			body = Prune(LoopUnswitchingRewriter.Apply(body, out var didUnswitch));

			// A declaration that used to sit inside one if-arm is, after the split, a direct child of
			// its own now-standalone loop — exactly the shape CSE and LICM (which never looks inside an
			// if) can act on but couldn't before the split.
			if (didUnswitch)
			{
				if (attribute.Optimizations.HasFlag(OptimizationFlags.CommonSubexpressionElimination))
				{
					body = Prune(CommonSubexpressionEliminator.Eliminate(body, attribute.MathOptimizations) ?? body);
				}

				if (attribute.Optimizations.HasFlag(OptimizationFlags.LoopInvariantCodeMotion))
				{
					body = Prune(LoopInvariantCodeMotionRewriter.Apply(body));
				}
			}
		}

		if (attribute.Optimizations.HasFlag(OptimizationFlags.LoopFusion))
		{
			body = Prune(LoopFusionRewriter.Apply(body, out var didFuse));

			// A subexpression duplicated only by the merge (each loop body had it once) cannot have
			// been caught by the earlier CSE pass, which never saw the fused body. The local CSE just
			// introduced for it is, in turn, a fresh direct child of the fused loop's block — a shape
			// the earlier LICM pass never saw either, since it ran before this declaration existed.
			if (didFuse)
			{
				if (attribute.Optimizations.HasFlag(OptimizationFlags.CommonSubexpressionElimination))
				{
					body = Prune(CommonSubexpressionEliminator.Eliminate(body, attribute.MathOptimizations) ?? body);
				}

				if (attribute.Optimizations.HasFlag(OptimizationFlags.LoopInvariantCodeMotion))
				{
					body = Prune(LoopInvariantCodeMotionRewriter.Apply(body));
				}
			}
		}

		if (attribute.Optimizations.HasFlag(OptimizationFlags.InductionVariableStrengthReduction))
		{
			body = Prune(StrengthReductionRewriter.Apply(body));
		}

		if (attribute.Optimizations.HasFlag(OptimizationFlags.TailRecursionElimination) && body is BlockSyntax recursiveBody)
		{
			// Wrapped in a stand-in declaration: the rewriter only reads the name and parameters off it.
			body = Prune(TailRecursionRewriter.Apply(MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), methodName)
				.WithParameterList(parameters)
				.WithBody(recursiveBody)));
		}

		// Runs last so the loop guard sees any loop tail-recursion elimination just introduced.
		if (attribute.Optimizations.HasFlag(OptimizationFlags.StackAllocConversion))
		{
			body = Prune(StackAllocRewriter.Apply(body));
		}

		// After stackalloc conversion, so the locals it turned into spans are picked up as spans.
		// No prune afterwards: this pass creates no dead code, and the pruner does not know ref locals.
		if (attribute.Optimizations.HasFlag(OptimizationFlags.BoundsCheckElimination))
		{
			body = BoundsCheckRewriter.Apply(body, parameters, variables);
		}

		// Last: the bool-to-int BitCast rewrite (unconditional, not gated by OptimizationFlags) always
		// keeps its outer widening cast at creation time, since it only sees the conditional's immediate
		// position — earlier passes above (e.g. single-use variable inlining, upstream in the main
		// rewriter) may since have relocated it into a position where the cast is now provably redundant.
		// Checking that here, on the fully-formed tree, is the only place it can be decided correctly.
		body = RedundantBitCastElisionRewriter.Apply(body, semanticModel, symbolStore);

		// Last of all: extraction reduces every remaining throw expression's static shape to an
		// invocation, which is exactly what the two passes above still need to see as ThrowExpressionSyntax.
		body = ThrowExpressionExtractionRewriter.Apply(body, additionalMethods, usings);

		return body;

		SyntaxNode Prune(SyntaxNode node) =>
			DeadCodePruner.Prune(node, variables, semanticModel);
	}
}
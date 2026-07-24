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
	                               ConstExprAttribute attribute, IDictionary<string, VariableItem> variables, SemanticModel semanticModel)
	{
		if (attribute.Optimizations.HasFlag(OptimizationFlags.CopyPropagation))
		{
			body = Prune(CopyPropagationRewriter.Apply(body));
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
			body = Prune(LoopUnswitchingRewriter.Apply(body));
		}

		if (attribute.Optimizations.HasFlag(OptimizationFlags.LoopFusion))
		{
			body = Prune(LoopFusionRewriter.Apply(body));
		}

		if (attribute.Optimizations.HasFlag(OptimizationFlags.IndexFromEndConversion))
		{
			body = Prune(IndexFromEndRewriter.Apply(body));
		}

		if (attribute.Optimizations.HasFlag(OptimizationFlags.InductionVariableStrengthReduction))
		{
			body = Prune(StrengthReductionRewriter.Apply(body));
		}

		if (attribute.Optimizations.HasFlag(OptimizationFlags.TailRecursionElimination) && body is BlockSyntax recursiveBody)
		{
			// Wrapped in a stand-in declaration: the rewriter only reads the name and parameters off it.
			body = TailRecursionRewriter.Apply(MethodDeclaration(PredefinedType(Token(SyntaxKind.VoidKeyword)), methodName)
				.WithParameterList(parameters)
				.WithBody(recursiveBody));
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

		return body;

		SyntaxNode Prune(SyntaxNode node) =>
			DeadCodePruner.Prune(node, variables, semanticModel);
	}
}
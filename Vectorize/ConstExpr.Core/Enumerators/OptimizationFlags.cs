using System;

namespace ConstExpr.Core.Enumerators;

/// <summary>
///   Controls general-purpose code optimization passes applied during constant expression evaluation.
///   These passes are independent of floating-point semantics and apply equally to integer,
///   string, and collection operations.
/// </summary>
/// <remarks>
///   Use <see cref="All" /> to enable every available pass, or combine individual flags for
///   fine-grained control. Set on <see cref="ConstExpr.Core.Attributes.ConstExprAttribute.Optimizations" />
///   (or its alias on <see cref="ConstExpr.Core.Attributes.ConstEvalAttribute.Optimizations" />).
///   For floating-point–specific relaxations see <see cref="FastMathFlags" />.
/// </remarks>
[Flags]
public enum OptimizationFlags
{
	/// <summary>
	///   No general optimization passes are applied (default).
	/// </summary>
	None = 0,

	/// <summary>
	///   Enable Common Subexpression Elimination (CSE).
	///   Identifies repeated sub-expressions and replaces subsequent occurrences with a local
	///   variable, avoiding redundant computation.
	/// </summary>
	CommonSubexpressionElimination = 1 << 0,

	/// <summary>
	///   Enable Loop Invariant Code Motion (LICM).
	///   Moves expressions whose value does not change across iterations to before the loop body,
	///   avoiding redundant work on every iteration.
	/// </summary>
	LoopInvariantCodeMotion = 1 << 1,

	/// <summary>
	///   Enable tail-recursion elimination (TRE).
	///   Rewrites tail-recursive methods into iterative <c>while</c>-loops, eliminating stack
	///   growth and the associated overhead.
	/// </summary>
	TailRecursionElimination = 1 << 2,

	/// <summary>
	///   Enable loop unswitching.
	///   When a loop body is a single <c>if</c> whose condition does not change across iterations,
	///   the condition is hoisted out and the loop is duplicated per branch, so the test runs once
	///   instead of on every iteration.
	/// </summary>
	LoopUnswitching = 1 << 3,

	/// <summary>
	///   Enable loop fusion.
	///   Two directly adjacent loops with identical iteration spaces and independent bodies are
	///   merged into one loop, so the loop overhead (counter, bound check) is paid once.
	/// </summary>
	LoopFusion = 1 << 4,

	/// <summary>
	///   Enable index-from-end conversion.
	///   Rewrites indexing off the end of a collection, such as <c>arr[arr.Length - 1 - i]</c>,
	///   into index-from-end syntax: <c>arr[^(1 + i)]</c>.
	/// </summary>
	IndexFromEndConversion = 1 << 5,

	/// <summary>
	///   Enable copy propagation.
	///   Replaces reads of a local that is a plain copy of another variable (<c>var y = x;</c>)
	///   with the source variable, so later passes (CSE, LICM) see one canonical name. The dead
	///   copy declaration is then removed by dead-code pruning.
	/// </summary>
	CopyPropagation = 1 << 6,

	/// <summary>
	///   Enable induction-variable strength reduction.
	///   Rewrites multiplication of a loop counter by an integer constant (<c>i * c</c>) into an
	///   accumulator advanced together with the counter, replacing a multiply per iteration with
	///   an add.
	/// </summary>
	InductionVariableStrengthReduction = 1 << 7,

	/// <summary>
	///   Enable stackalloc conversion.
	///   Rewrites a local heap array into a <c>Span&lt;T&gt;</c> backed by <c>stackalloc</c>
	///   (<c>var b = new int[256];</c> => <c>Span&lt;int&gt; b = stackalloc int[256];</c>) when the
	///   element type is a predefined unmanaged primitive, the size is a small compile-time constant,
	///   the declaration is not inside a loop, and every use is stack-safe (indexing, <c>.Length</c>,
	///   <c>foreach</c>, or <c>new string(b)</c>). Eliminates the heap allocation for throwaway
	///   local buffers.
	/// </summary>
	StackAllocConversion = 1 << 8,

	/// <summary>
	///   Enable all general-purpose optimization passes.
	///   Combines <see cref="CommonSubexpressionElimination" />, <see cref="LoopInvariantCodeMotion" />,
	///   <see cref="TailRecursionElimination" />, <see cref="LoopUnswitching" />, <see cref="LoopFusion" />,
	///   <see cref="IndexFromEndConversion" />, <see cref="CopyPropagation" />,
	///   <see cref="InductionVariableStrengthReduction" />, and <see cref="StackAllocConversion" />.
	/// </summary>
	All = CommonSubexpressionElimination | LoopInvariantCodeMotion | TailRecursionElimination | LoopUnswitching | LoopFusion | IndexFromEndConversion | CopyPropagation | InductionVariableStrengthReduction | StackAllocConversion
}
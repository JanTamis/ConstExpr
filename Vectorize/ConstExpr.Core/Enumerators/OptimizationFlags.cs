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
	///   Enable all general-purpose optimization passes.
	///   Combines <see cref="CommonSubexpressionElimination" />, <see cref="LoopInvariantCodeMotion" />,
	///   and <see cref="TailRecursionElimination" />.
	/// </summary>
	All = CommonSubexpressionElimination | LoopInvariantCodeMotion | TailRecursionElimination
}
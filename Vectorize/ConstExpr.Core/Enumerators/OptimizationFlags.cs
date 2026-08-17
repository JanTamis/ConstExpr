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
	///   Enable copy propagation.
	///   Replaces reads of a local that is a plain copy of another variable (<c>var y = x;</c>)
	///   with the source variable, so later passes (CSE, LICM) see one canonical name. The dead
	///   copy declaration is then removed by dead-code pruning.
	/// </summary>
	CopyPropagation = 1 << 5,

	/// <summary>
	///   Enable induction-variable strength reduction.
	///   Rewrites multiplication of a loop counter by an integer constant (<c>i * c</c>) into an
	///   accumulator advanced together with the counter, replacing a multiply per iteration with
	///   an add.
	/// </summary>
	InductionVariableStrengthReduction = 1 << 6,

	/// <summary>
	///   Enable stackalloc conversion.
	///   Rewrites a local heap array into a <c>Span&lt;T&gt;</c> backed by <c>stackalloc</c>
	///   (<c>var b = new int[256];</c> => <c>Span&lt;int&gt; b = stackalloc int[256];</c>) when the
	///   element type is a predefined unmanaged primitive, the size is a small compile-time constant,
	///   the declaration is not inside a loop, and every use is stack-safe (indexing, <c>.Length</c>,
	///   <c>foreach</c>, or <c>new string(b)</c>). Eliminates the heap allocation for throwaway
	///   local buffers.
	/// </summary>
	StackAllocConversion = 1 << 7,

	/// <summary>
	///   Enable bounds-check elimination.
	///   Rewrites array indexing (<c>arr[i]</c>) into direct reference arithmetic
	///   (<c>Unsafe.Add(ref MemoryMarshal.GetArrayDataReference(arr), (nuint) i)</c>), so the runtime
	///   no longer range-checks the index.
	///   <para>
	///     This pass does <em>not</em> prove that indices stay in range — that guarantee is the
	///     caller's, exactly as with <c>-fno-bounds-check</c> in a C compiler. An out-of-range index
	///     silently reads or writes adjacent heap memory instead of throwing
	///     <see cref="System.IndexOutOfRangeException" />. Enable it only on code whose indexing is
	///     known to be correct.
	///   </para>
	///   <para>
	///     The generated code calls <c>MemoryMarshal.GetArrayDataReference</c> and so requires
	///     .NET 5 or later.
	///   </para>
	/// </summary>
	BoundsCheckElimination = 1 << 8,

	/// <summary>
	///   Enable value-range propagation (VRP).
	///   Derives the interval each integer expression is known to fall in — from literals, from a local's
	///   initializer, from an ascending <c>for</c> header, and from a guard that dominates the use — and
	///   folds a comparison whose outcome those intervals already settle into <c>true</c> or
	///   <c>false</c>, dropping the branch that can no longer be taken:
	///   <code>
	///   for (var i = 0; i &lt; n; i++)           for (var i = 0; i &lt; n; i++)
	///       if (i >= 0) sum += data[i];   =>       sum += data[i];
	///   </code>
	///   <para>
	///     Only provably dead code is removed, so the pass cannot change what the method computes. It
	///     does not narrow types, elide overflow checks, or affect
	///     <see cref="BoundsCheckElimination" />, which keeps proving nothing about its indices.
	///   </para>
	/// </summary>
	ValueRangePropagation = 1 << 9,

	/// <summary>
	///   Enable default-branch hoisting.
	///   When a declared local is immediately followed by an <c>if</c>/<c>else</c> whose <c>then</c>
	///   branch is nothing but a straight-line assignment of every one of those locals to a
	///   side-effect-free value, that branch becomes the locals' initializer and the condition is
	///   negated, dropping the now-redundant branch entirely:
	///   <code>
	///   double r, g, b;                    var r = v;
	///   if (s == 0)                        var g = v;
	///       r = v; g = v; b = v;    =>      var b = v;
	///   else                                if (s != 0)
	///       ...                                 ...
	///   </code>
	/// </summary>
	DefaultBranchHoisting = 1 << 10,

	/// <summary>
	///   Enable additive reassociation.
	///   Flattens a chain of <c>+</c>/<c>-</c> operators, collapses repeated integer terms into a
	///   single scaled term, and sums the chain's literal operands into one trailing constant:
	///   <code>
	///   x + 10 + x - 5  =>  (x &lt;&lt; 1) + 5
	///   </code>
	///   Limited to terms that resolve to an integer type and can have no side effect — reordering
	///   those is always exact, unlike floating-point, where it can shift rounding (see
	///   <see cref="FastMathFlags.AssociativeMath" />).
	/// </summary>
	Reassociation = 1 << 11,

	/// <summary>
	///   Enable while-to-do-while conversion.
	///   When a <c>while</c> loop's condition is proven true on the very first check — the same
	///   interval analysis <see cref="ValueRangePropagation" /> uses, applied to the loop's entry state
	///   rather than to a fixed point inside it — the loop is rewritten as a <c>do</c>-<c>while</c>,
	///   dropping the now-redundant initial test:
	///   <code>
	///   var i = 1;                         var i = 1;
	///   while (i &lt;= n) { ... i++; }   =>   do { ... i++; } while (i &lt;= n);
	///   </code>
	///   Only the loop keyword changes — the condition expression itself is left exactly as written and
	///   is still evaluated on every iteration, so this never risks the infinite-loop hazard
	///   <see cref="ValueRangePropagation" /> guards against when folding a loop condition outright.
	/// </summary>
	WhileToDoWhileConversion = 1 << 12,

	/// <summary>
	///   Enable nullable-annotation-driven simplification.
	///   Treats an unannotated (non-<c>?</c>) reference-type expression as provably non-null, and lets
	///   code whose only extra work is a null check collapse to what remains. Two places consume this:
	///   the per-invocation string function optimizers —
	///   <code>
	///   string.IsNullOrEmpty(s)       =>  s.Length == 0
	///   string.IsNullOrWhiteSpace(s)  =>  s.AsSpan().IsWhiteSpace()
	///   </code>
	///   — and the general expression rewriter, which folds:
	///   <code>
	///   x ?? y        =>  x
	///   x?.Foo()      =>  x.Foo()
	///   x == null     =>  false        x != null     =>  true
	///   x is null     =>  false        x is not null =>  true
	///   x ??= y       =>  x            (or drops the statement entirely when used standalone)
	///   </code>
	///   <para>
	///     This only ever removes a null check the caller has already proven unnecessary — it never
	///     narrows a type or changes what the method computes for any input that was reachable before.
	///     The <c>string.IsNullOrWhiteSpace</c> rewrite additionally needs
	///     <c>MemoryExtensions.IsWhiteSpace(ReadOnlySpan&lt;char&gt;)</c> (.NET 8+) and is skipped when
	///     that API isn't available in the target compilation.
	///   </para>
	/// </summary>
	UseNullableAnnotations = 1 << 13,

	/// <summary>
	///   Enable Max/Min scale-factor distribution.
	///   When two or more locals are each declared as a parameter (or expression) scaled by the same
	///   positive constant (<c>var dr = r * K;</c>) and combined via a <c>Max</c>/<c>Min</c> chain,
	///   distributes the constant out of the chain (<c>Max(a*K, b*K) =&gt; Max(a,b) * K</c>). When the
	///   chain's complement then also appears as the denominator of a division whose numerator is
	///   built from the same scaled locals (a "normalize, take extremum, ratio against the complement"
	///   idiom), cancels the shared constant out of that division too, leaving a plain ratio of the
	///   un-scaled operands:
	///   <code>
	///   var dr = r * K;                    var max = Max(Max(r, g), b);
	///   var dg = g * K;                     var k = 1D - max * K;
	///   var db = b * K;             =&gt;
	///   var k = 1D - Max(Max(dr,dg),db);
	///   var c = (1D - dr - k) / (1D - k);   var c = (max - r) / max;
	///   </code>
	/// </summary>
	ScaleFactorDistribution = 1 << 14,

	/// <summary>
	///   Enable all general-purpose optimization passes.
	///   Combines <see cref="CommonSubexpressionElimination" />, <see cref="LoopInvariantCodeMotion" />,
	///   <see cref="TailRecursionElimination" />, <see cref="LoopUnswitching" />, <see cref="LoopFusion" />, <see cref="CopyPropagation" />,
	///   <see cref="InductionVariableStrengthReduction" />, <see cref="StackAllocConversion" />, <see cref="BoundsCheckElimination"/>,
	///   <see cref="ValueRangePropagation" />, <see cref="DefaultBranchHoisting" />, <see cref="Reassociation" />,
	///   <see cref="WhileToDoWhileConversion" />, <see cref="UseNullableAnnotations" /> and <see cref="ScaleFactorDistribution" />.
	/// </summary>
	All = CommonSubexpressionElimination | LoopInvariantCodeMotion | TailRecursionElimination | LoopUnswitching | LoopFusion | CopyPropagation | InductionVariableStrengthReduction | BoundsCheckElimination | StackAllocConversion | ValueRangePropagation | DefaultBranchHoisting | Reassociation | WhileToDoWhileConversion | UseNullableAnnotations | ScaleFactorDistribution
}
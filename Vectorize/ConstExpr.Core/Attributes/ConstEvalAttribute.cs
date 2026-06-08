using System;
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Core.Attributes;

/// <summary>
/// Alias for <see cref="ConstExprAttribute"/>. Marks a method, constructor, or type as eligible
/// for constant expression evaluation performed by the ConstExpr tooling or analyzers.
/// </summary>
/// <remarks>
/// <c>[ConstEval]</c> and <c>[ConstExpr]</c> are interchangeable — both trigger the same
/// compile-time evaluation pipeline.
/// Use <see cref="MathOptimizations"/> to control floating-point semantics (strict vs fast).
/// Use <see cref="Optimizations"/> to enable general code optimization passes (CSE, LICM, TRE).
/// Use <see cref="LinqOptimisationMode"/> to control whether LINQ calls are folded, unrolled,
/// or left unchanged.
/// Use <see cref="MaxUnrollIterations"/> to cap the number of loop iterations that may be unrolled.
/// </remarks>
/// <seealso cref="ConstExprAttribute"/>
/// <seealso cref="FastMathFlags"/>
/// <seealso cref="OptimizationFlags"/>
/// <seealso cref="LinqOptimisationMode"/>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ConstEvalAttribute(
	FastMathFlags mathOptimizations = FastMathFlags.Strict,
	LinqOptimisationMode linqOptimisationMode = LinqOptimisationMode.Optimize,
	uint maxUnrollIterations = 32,
	OptimizationFlags optimizations = OptimizationFlags.None) : Attribute
{
	/// <summary>
	/// Gets or sets the fast-math flags used during constant expression processing.
	/// </summary>
	/// <value>
	/// Defaults to <see cref="FastMathFlags.Strict"/> (full IEEE 754 compliance).
	/// Use <see cref="FastMathFlags.FastMath"/> to enable all floating-point relaxations at once,
	/// or combine individual <see cref="FastMathFlags"/> values for fine-grained control.
	/// For general code optimization passes (CSE, LICM, TRE) see <see cref="Optimizations"/>.
	/// </value>
	public FastMathFlags MathOptimizations { get; set; } = mathOptimizations;

	/// <summary>
	///   Gets or sets the general code optimization passes applied during constant expression evaluation.
	/// </summary>
	/// <value>
	///   Defaults to <see cref="OptimizationFlags.None" />.
	///   Use <see cref="OptimizationFlags.All" /> to enable every pass, or combine individual flags:
	///   <list type="bullet">
	///     <item>
	///       <description>
	///         <see cref="OptimizationFlags.CommonSubexpressionElimination" /> — eliminates repeated
	///         sub-expressions.
	///       </description>
	///     </item>
	///     <item>
	///       <description>
	///         <see cref="OptimizationFlags.LoopInvariantCodeMotion" /> — hoists loop-invariant expressions out of
	///         loops.
	///       </description>
	///     </item>
	///     <item>
	///       <description>
	///         <see cref="OptimizationFlags.TailRecursionElimination" /> — rewrites tail-recursive calls into
	///         iterative loops.
	///       </description>
	///     </item>
	///   </list>
	///   These passes are independent of floating-point semantics; see <see cref="MathOptimizations" /> for IEEE 754
	///   relaxations.
	/// </value>
	public OptimizationFlags Optimizations { get; set; } = optimizations;

	/// <summary>
	/// Gets or sets the maximum number of loop iterations to unroll during constant expression evaluation.
	/// </summary>
	/// <value>
	/// Defaults to 32. Set to 0 to disable loop unrolling entirely.
	/// </value>
	public uint MaxUnrollIterations { get; set; } = maxUnrollIterations;

	/// <summary>
	/// Gets or sets how LINQ method calls are handled during constant expression evaluation.
	/// </summary>
	/// <value>
	/// Defaults to <see cref="Enumerators.LinqOptimisationMode.Optimize"/>.
	/// <list type="bullet">
	///   <item><description><see cref="Enumerators.LinqOptimisationMode.None"/> — LINQ calls are left as-is.</description></item>
	///   <item><description><see cref="Enumerators.LinqOptimisationMode.Optimize"/> — LINQ calls on constant collections
	///     are folded to literals where possible.</description></item>
	///   <item><description><see cref="Enumerators.LinqOptimisationMode.Unroll"/> — LINQ chains are additionally unrolled
	///     into imperative code via <c>LinqUnroller</c>.</description></item>
	/// </list>
	/// </value>
	public LinqOptimisationMode LinqOptimisationMode { get; set; } = linqOptimisationMode;
}
using ConstExpr.Core.Enumerators;
using System;

namespace ConstExpr.Core.Attributes;

/// <summary>
/// Alias for <see cref="ConstExprAttribute"/>. Marks a method, constructor, or type as eligible
/// for constant expression evaluation performed by the ConstExpr tooling or analyzers.
/// </summary>
/// <remarks>
/// <c>[ConstEval]</c> and <c>[ConstExpr]</c> are interchangeable — both trigger the same
/// compile-time evaluation pipeline.
/// Use <see cref="FloatingPointMode"/> to control floating‑point semantics (strict vs fast).
/// Use <see cref="LinqOptimisationMode"/> to control whether LINQ calls are folded, unrolled,
/// or left unchanged.
/// Use <see cref="MaxUnrollIterations"/> to cap the number of loop iterations that may be unrolled.
/// </remarks>
/// <seealso cref="ConstExprAttribute"/>
/// <seealso cref="FloatingPointEvaluationMode"/>
/// <seealso cref="LinqOptimisationMode"/>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ConstEvalAttribute(
  FloatingPointEvaluationMode floatingPointMode = FloatingPointEvaluationMode.Strict,
  LinqOptimisationMode linqOptimisationMode = LinqOptimisationMode.Optimize,
  uint maxUnrollIterations = 32) : Attribute
{
	/// <summary>
	/// Gets or sets the floating‑point evaluation mode used during constant expression processing.
	/// </summary>
	/// <value>
	/// Defaults to <see cref="FloatingPointEvaluationMode.Strict"/>.
	/// When set to <see cref="FloatingPointEvaluationMode.FastMath"/>, additional math optimisations
	/// are applied (e.g. char-overload promotion and fast math method folding).
	/// </value>
	public FloatingPointEvaluationMode FloatingPointMode { get; set; } = floatingPointMode;

	/// <summary>
	/// Gets or sets the maximum number of loop iterations to unroll during constant expression evaluation.
	/// </summary>
	/// <value>
	/// Defaults to 32. Set to 0 to disable loop unrolling entirely
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

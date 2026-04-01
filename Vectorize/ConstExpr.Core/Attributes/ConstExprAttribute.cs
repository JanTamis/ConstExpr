using ConstExpr.Core.Enumerators;
using System;

namespace ConstExpr.Core.Attributes;

/// <summary>
/// Marks a method, constructor, or type as eligible for constant expression evaluation
/// performed by the ConstExpr tooling or analyzers.
/// </summary>
/// <remarks>
/// Applying this attribute signals that the annotated member (or all members inside an
/// annotated type) should be considered for compile-time evaluation when possible.
/// Use <see cref="MathOptimizations"/> to control floating‑point semantics (strict vs fast).
/// Use <see cref="LinqOptimisationMode"/> to control whether LINQ calls are folded, unrolled
/// </remarks>
/// <seealso cref="FastMathFlags"/>
/// <seealso cref="LinqOptimisationMode"/>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ConstExprAttribute(
	FastMathFlags mathOptimizations = FastMathFlags.Strict,
	LinqOptimisationMode linqOptimisationMode = LinqOptimisationMode.Optimize,
	uint maxUnrollIterations = 32) : Attribute
{
	/// <summary>
	/// Gets or sets the fast-math flags used during constant expression processing.
	/// </summary>
	/// <value>
	/// Defaults to <see cref="FastMathFlags.Strict"/>.
	/// When set to <see cref="FastMathFlags.FastMath"/>, additional math optimisations
	/// are applied (e.g. char-overload promotion and fast math method folding).
	/// Individual flags from <see cref="FastMathFlags"/> can also be combined for fine-grained control.
	/// </value>
	public FastMathFlags MathOptimizations { get; set; } = mathOptimizations;

	/// <summary>
	/// Gets or sets the maximum number of loop iterations to unroll during constant expression evaluation.
	/// </summary>
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
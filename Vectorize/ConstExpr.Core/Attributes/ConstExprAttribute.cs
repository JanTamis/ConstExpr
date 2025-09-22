using System;

namespace ConstExpr.Core.Attributes;

/// <summary>
/// Marks a method, constructor, or type as eligible for constant expression evaluation
/// performed by the ConstExpr tooling or analyzers.
/// </summary>
/// <remarks>
/// Applying this attribute signals that the annotated member (or all members inside an
/// annotated type) should be considered for compile-time evaluation when possible.
/// The <see cref="IEEE754Compliant"/> property can be used to opt in or out of strict
/// IEEE 754 compliance for floating‑point operations during that evaluation.
/// </remarks>
/// <seealso cref="IEEE754Compliant"/>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Constructor | AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class ConstExprAttribute : Attribute
{
	/// <summary>
	/// Gets or sets a value indicating whether floating‑point evaluation should adhere
	/// strictly to IEEE 754 semantics (e.g., respecting NaN propagation, rounding,
	/// and exceptional cases) during constant expression processing.
	/// </summary>
	/// <value>
	/// <c>true</c> to enforce IEEE 754–compliant behavior (default); <c>false</c> to allow
	/// potential deviations where an implementation may use faster, non‑strict evaluations.
	/// </value>
	public bool IEEE754Compliant { get; set; } = true;
}


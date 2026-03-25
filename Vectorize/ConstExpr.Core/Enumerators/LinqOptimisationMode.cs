namespace ConstExpr.Core.Enumerators;

/// <summary>
/// Controls how LINQ method calls are handled during compile-time constant expression evaluation.
/// </summary>
/// <seealso cref="ConstExpr.Core.Attributes.ConstExprAttribute.LinqOptimisationMode"/>
public enum LinqOptimisationMode
{
	/// <summary>
	/// LINQ optimisation is disabled. LINQ calls are left as-is and are never folded or unrolled.
	/// </summary>
	None,

	/// <summary>
	/// LINQ calls are optimised where possible (e.g. <c>Count()</c>, <c>First()</c>, <c>Where()</c> on
	/// constant collections are folded to literals), but loop unrolling is not performed.
	/// </summary>
	Optimize,

	/// <summary>
	/// LINQ calls are first optimised and then the resulting chain is unrolled into imperative code
	/// via <c>LinqUnroller</c>, eliminating all remaining LINQ overhead at the call site.
	/// </summary>
	Unroll,
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   A `for` loop whose bound is a compile-time constant larger than
///   <c>ConstExprAttribute.MaxUnrollIterations</c> (default 32) hits the unroll cap partway through and
///   falls back to a plain (non-unrolled) loop. The loop counter and body-local <c>bonus</c> were
///   already registered once by the partial-unroll attempt; re-visiting the original declarations
///   without resetting that state used to throw an <see cref="InvalidCastException" /> deep inside
///   <c>base.VisitForStatement</c> (swallowed by the rewriter's top-level exception handler), which
///   discarded all folding for the loop — <c>bonus</c> stayed as the unfolded <c>3 + 4</c> instead of
///   being resolved to <c>7</c>.
/// </summary>
[InheritsTests]
public class ValueRangeLargeConstantBoundUnrollCapTest() : BaseTestWithRandomValues<Func<int, int>>(optimizations: OptimizationFlags.None, maxUnrollIterations: 100)
{
	public override string TestMethod => GetString(n =>
	{
		var sum = 0;

		for (var i = 0; i < 100; i++)
		{
			var bonus = 3 + 4;
			sum += i + n + bonus;
		}

		return sum;
	});

	// With maxUnrollIterations raised to 100 (see constructor above), the 100-iteration loop no
	// longer hits the unroll cap - even with n Unknown, every iteration's bonus-plus-i part still
	// folds per-iteration, so the loop unrolls into 100 flat `sum += n + <constant>;` statements
	// instead of staying a compact for loop.
	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(BuildUnrolledExpectedBody(), Unknown)
	];

	private static string BuildUnrolledExpectedBody()
	{
		var statements = string.Join("\n", Enumerable.Range(7, 100).Select(v => $"sum += n + {v};"));

		return $"var sum = 0;\n{statements}\nreturn sum;";
	}
}
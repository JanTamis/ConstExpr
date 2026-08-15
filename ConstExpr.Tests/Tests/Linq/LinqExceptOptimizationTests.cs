namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Except() optimization - verify that redundant operations and special cases are optimized
/// </summary>
[InheritsTests]
public class LinqExceptOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
{
	// One of the suite's heaviest random tests: each draw runs the full rewriter over a body of ten-plus
	// LINQ chains, measured at ~0.45 s per draw. Halved from the default 10 - the per-draw cost is set by
	// the body being rewritten, not by the input, so fewer draws is the only knob that moves it.
	protected override int RandomTestCaseCount => 5;

	// Yields 4-5 checked cases at 5 draws (measured); floor leaves margin so a future seed or generator
	// change that starves this class fails loudly instead of quietly checking one.
	protected override int MinRandomTestCaseCount => 3;

	public override string TestMethod => GetString(x =>
	{
		// Except with Empty => Distinct() (removing nothing)
		var a = x.Except([ ]).Count();

		// Empty.Except(collection) => Empty (empty minus anything is empty)
		var b = Enumerable.Empty<int>().Except(x).Count();

		// collection.Except(collection) => Empty (set minus itself)
		var c = x.Except(x).Count();

		// AsEnumerable().Except() => Except() (skip type cast)
		var d = x.AsEnumerable().Except([ 1 ]).Count();

		// ToList().Except() => Except() (skip materialization)
		var e = x.ToList().Except([ 2 ]).Count();

		// ToArray().Except() => Except() (skip materialization)
		var f = x.ToArray().Except([ 3 ]).Count();

		// Distinct().Except() => Except() (Except already applies Distinct)
		var g = x.Distinct().Except([ 1, 2 ]).Count();

		// Multiple skip operations
		var h = x.AsEnumerable().ToList().Except([ 4 ]).Count();

		// Chained Except: Except(a).Except(b) => Except(a.Concat(b))
		var i = x.Except([ 1 ]).Except([ 2 ]).Count();

		// Chained Except with 3 operations
		var j = x.Except([ 1 ]).Except([ 2 ]).Except([ 3 ]).Count();

		// OrderBy().Except().Count() => Except().Count() (Count is set-based)
		var k = x.OrderBy(v => v).Except([ 1 ]).Count();

		// Reverse().Except().Any() => Except().Any() (Any is set-based)
		var l = x.Reverse().Except([ 5 ]).Any() ? 1 : 0;

		// Except on both sides optimized
		var m = x.Distinct().Except(new[] { 1, 2 }.ToList()).Count();

		// Regular Except (should not be further optimized)
		var n = x.Except([ 99 ]).Count();

		return a + b + c + d + e + f + g + h + i + j + k + l + m + n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (Count_wX25Rw(x) << 1) + Count_vFVZUg(x) * 3 + Count_IyhE7Q(x) + Count_lIg1kw(x) + Count_4oc4tg(x) + Count_87tGZw(x) + Unsafe.BitCast<bool, byte>(VectorOperations.Any<int, Operator_sOSqQ>(x)) + Count_uLsNyg(x);"),
	];
}
namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Zip() optimization - verify empty collection handling
/// </summary>
[InheritsTests]
public class LinqZipOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
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
		// Zip with empty => empty
		var a = x.Zip(Enumerable.Empty<int>()).Count();

		// Empty.Zip(collection) => empty
		var b = Enumerable.Empty<int>().Zip(x).Count();

		var c = x.Zip(x).Count();

		var d = x.Zip(x.Where(w => w > 0)).Count();

		return a + b + c + d;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var xLength = x.Length;

			return xLength + Int32.Min(xLength, Count_Pdf8bA(x));
			"""),
	];
}
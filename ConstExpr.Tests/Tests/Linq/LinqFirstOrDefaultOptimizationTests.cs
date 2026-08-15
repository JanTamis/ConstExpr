namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for FirstOrDefault() optimization - verify that unnecessary operations before FirstOrDefault() are removed
/// </summary>
[InheritsTests]
public class LinqFirstOrDefaultOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
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
		// Where(...).FirstOrDefault() => FirstOrDefault(predicate)
		var a = x.Where(v => v > 3).FirstOrDefault();

		// AsEnumerable().FirstOrDefault() => FirstOrDefault()
		var b = x.AsEnumerable().FirstOrDefault();

		// ToList().FirstOrDefault() => FirstOrDefault()
		var c = x.ToList().FirstOrDefault();

		// ToArray().FirstOrDefault() => FirstOrDefault()
		var d = x.ToArray().FirstOrDefault();

		// AsEnumerable().Where().FirstOrDefault() => FirstOrDefault(predicate)
		var e = x.AsEnumerable().Where(v => v > 2).FirstOrDefault();

		// ToList().Where().FirstOrDefault() => FirstOrDefault(predicate)
		var f = x.ToList().Where(v => v < 5).FirstOrDefault();

		// Complex: AsEnumerable().ToList().Where().FirstOrDefault() => FirstOrDefault(predicate)
		var g = x.AsEnumerable().ToList().Where(v => v == 3).FirstOrDefault();

		// OrderBy should NOT be optimized (changes which element is first!)
		var h = x.OrderBy(v => v).FirstOrDefault();

		// Reverse should NOT be optimized (changes which element is first!)
		var i = x.Reverse().FirstOrDefault();

		// Distinct should NOT be optimized (first element might be duplicate!)
		var j = x.Distinct().FirstOrDefault();

		// Array conditional: x.FirstOrDefault() => x.Length > 0 ? x[0] : default
		var k = x.FirstOrDefault();

		var l = x.Where(v => v > 0).Select(s => s * 2).FirstOrDefault();

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			ref var xRef = ref MemoryMarshal.GetArrayDataReference(x);

			var xLength = x.Length;
			var gt = xLength > 0;

			return (Array.Find(x, v => v > 0) << 1) + (gt ? xRef * 5 : 0) + Array.Find(x, v => v > 3) + Array.Find(x, v => v > 2) + Array.Find(x, v => v < 5) + Array.Find(x, v => v == 3) + TensorPrimitives.Min(x) + (gt ? Unsafe.Add(ref xRef, xLength - 1) : 0);
			"""),
	];
}
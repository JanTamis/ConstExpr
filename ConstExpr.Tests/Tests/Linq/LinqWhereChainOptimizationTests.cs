using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Where() optimization with multiple chained Where statements
/// </summary>
[InheritsTests]
public class LinqWhereChainOptimizationTests() : BaseTestWithRandomValues<Func<int[], IEnumerable<int>>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision)
{
	// Heavy (~0.45 s per draw), but deliberately NOT cut to 5 like its siblings: its filters reject most
	// random ints, so draws collide on the same expected body and 5 draws yield only 1 checked case where
	// 10 yield 3. Measured, not assumed.
	protected override int MinRandomTestCaseCount => 2;

	public override string TestMethod => GetString(x =>
	{
		// Two Where statements
		var a = x.Where(v => v > 2).Where(v => v < 10);

		// Three Where statements
		var b = x.Where(v => v > 1).Where(v => v < 8).Where(v => v % 2 == 0);

		// Four Where statements
		var c = x.Where(v => v > 0).Where(v => v < 100).Where(v => v % 3 == 0).Where(v => v < 50);

		// Where with different parameter names
		var d = x.Where(p => p > 5).Where(q => q < 15);

		// Where(true) should be removed in chain
		var e = x.Where(_ => true).Where(v => v > 3).Where(v => v < 7);

		// Where(false) should make entire chain empty
		var f = x.Where(v => v > 1).Where(_ => false).Where(v => v < 10);

		return a.Concat(b).Concat(c).Concat(d).Concat(e).Concat(f);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x
			.Where(v => (uint) (v - 3) <= 6U)
			.Concat(x.Where(v => (uint) (v - 2) <= 5U && Int32.IsEvenInteger(v)))
			.Concat(x.Where(v => (uint) (v - 1) <= 98U && v - ((int) (v * 1431655766L >> 32) - (v >> 31)) * 3 == 0 && v < 50))
			.Concat(x.Where(p => (uint) (p - 6) <= 8U))
			.Concat(x.Where(v => (uint) (v - 4) <= 2U)))
	];
}
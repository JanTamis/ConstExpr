namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Single() optimization - verify Where fusion and chain optimization
/// </summary>
[InheritsTests]
public class LinqSingleOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
{
	// Single() needs exactly one element equal to 3 and exactly one equal to 2. Over the full int range that
	// never happened, so every draw threw and was discarded - the random pass checked nothing at all. Capped
	// to 0-3 so 2 and 3 are drawn from a small enough alphabet to actually land exactly once.
	// Note the hard ceiling of one checked case here, so MinRandomTestCaseCount stays at its default: the
	// matches are pinned to 3 and 2 by the predicates, so a surviving draw always folds to `return 5;`.
	protected override int MaxRandomMagnitudeBits => 2;


	public override string TestMethod => GetString(x =>
	{
		// Where(predicate).Single() => Single(predicate)
		var a = x.Where(v => v == 3).Single();

		// AsEnumerable().ToList().Where(predicate).Single() => Single(predicate)
		var b = x.AsEnumerable().ToList().Where(v => v == 2).Single();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Single_nJEiIg(x) + Single_A6_ZQQ(x);"),
	];
}
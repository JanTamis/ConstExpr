namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Single() optimization - verify Where fusion and chain optimization
/// </summary>
[InheritsTests]
public class LinqSingleOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(predicate).Single() => Single(predicate)
		var a = x.Where(v => v == 3).Single();

		// AsEnumerable().ToList().Where(predicate).Single() => Single(predicate)
		var b = x.AsEnumerable().ToList().Where(v => v == 2).Single();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Single(v => v == 3);
			var b = x.Single(v => v == 2);

			return a + b;
			""", Unknown),
		Create("return 5;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 5;", new[] { 2, 3 }),
	];
}

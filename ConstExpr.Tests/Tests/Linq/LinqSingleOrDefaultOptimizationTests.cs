namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for SingleOrDefault() optimization - verify Where fusion and chain optimization
/// </summary>
[InheritsTests]
public class LinqSingleOrDefaultOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(predicate).SingleOrDefault() => SingleOrDefault(predicate)
		var a = x.Where(v => v == 3).SingleOrDefault();

		// AsEnumerable().ToList().Where(predicate).SingleOrDefault() => SingleOrDefault(predicate)
		var b = x.AsEnumerable().ToList().Where(v => v == 99).SingleOrDefault();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return SingleOrDefault_BowQCQ(x) + SingleOrDefault_PHBkLg(x);"),
		Create(_ => 3, [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => 0, [ new[] { 1, 2, 4, 5 } ]),
	];
}
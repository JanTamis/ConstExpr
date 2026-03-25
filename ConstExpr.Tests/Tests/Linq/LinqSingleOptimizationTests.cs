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
			var a = Single_UeGPpQ(x);
			var b = Single_UeGPpQ(x);
			
			return a + b;
			""", Unknown),
		Create("return 5;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 5;", new[] { 2, 3 }),
	];
}

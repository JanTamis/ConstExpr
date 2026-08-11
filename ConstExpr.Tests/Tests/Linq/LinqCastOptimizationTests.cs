namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Cast() optimization - verify that AsEnumerable, ToList, ToArray are skipped
/// </summary>
[InheritsTests]
public class LinqCastOptimizationTests : BaseTestWithRandomValues<Func<List<object>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// AsEnumerable().Cast<int>() => collection.Cast<int>() (skip AsEnumerable)
		var a = x.AsEnumerable().Cast<int>().Sum();

		// ToList().Cast<int>() => collection.Cast<int>() (skip ToList)
		var b = x.ToList().Cast<int>().Sum();

		// ToArray().Cast<int>() => collection.Cast<int>() (skip ToArray)
		var c = x.ToArray().Cast<int>().Sum();

		// Multiple skip operations
		var d = x.AsEnumerable().ToList().Cast<int>().Sum();

		// Regular Cast (should not be optimized)
		var e = x.Cast<int>().Sum();

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Sum_gqdmOQ(x) * 5;"),
	];
}
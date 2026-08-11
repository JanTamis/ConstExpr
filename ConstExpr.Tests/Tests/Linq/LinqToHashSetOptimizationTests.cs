namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for ToHashSet() optimization - verify redundant operations removal and chain optimization
/// </summary>
[InheritsTests]
public class LinqToHashSetOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// ToHashSet().ToHashSet() => ToHashSet()
		var a = x.ToHashSet().ToHashSet().Count;

		// Distinct().ToHashSet() => ToHashSet()
		var b = x.Distinct().ToHashSet().Count;

		// AsEnumerable().ToList().Distinct().ToHashSet() => ToHashSet()
		var c = x.AsEnumerable().ToList().Distinct().ToHashSet().Count;

		return a + b + c;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return ToHashSet_JgdI5A(x).Count * 3;"),
	];
}
namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for ToHashSet() optimization - verify redundant operations removal and chain optimization
/// </summary>
[InheritsTests]
public class LinqToHashSetOptimizationTests : BaseTest<Func<int[], int>>
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
// 		Create("""
// 			var a = x.Disctinct().Count();
// 			var b = x.Disctinct().Count();
// 			var c = x.Disctinct().Count();
//
// 			return a + b + c;
// 			""", Unknown),
		Create("return 9;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}


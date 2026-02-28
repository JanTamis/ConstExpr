namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for ToList() optimization - verify redundant materialization removal and chain optimization
/// </summary>
[InheritsTests]
public class LinqToListOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// ToList().ToList() => ToList()
		var a = x.ToList().ToList().Count;

		// ToArray().ToList() => ToList()
		var b = x.ToArray().ToList().Count;

		// AsEnumerable().ToArray().ToList() => ToList()
		var c = x.AsEnumerable().ToArray().ToList().Count;

		return a + b + c;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
// 		Create("""
// 			var a = x.Count();
// 			var b = x.Count();
// 			var c = x.Count();
//
// 			return a + b + c;
// 			""", Unknown),
		// Create("return 9;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}


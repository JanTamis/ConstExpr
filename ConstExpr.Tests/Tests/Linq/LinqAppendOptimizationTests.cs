namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Append() optimization - verify that AsEnumerable, ToList, ToArray are skipped
/// </summary>
[InheritsTests]
public class LinqAppendOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// AsEnumerable().Append() => collection.Append() (skip AsEnumerable)
		var a = x.AsEnumerable().Append(20).Sum();

		// ToList().Append() => collection.Append() (skip ToList)
		var b = x.ToList().Append(30).Sum();

		// ToArray().Append() => collection.Append() (skip ToArray)
		var c = x.ToArray().Append(40).Sum();

		// Multiple skip operations
		var d = x.AsEnumerable().ToList().Append(50).Sum();

		// Regular Append (should not be optimized)
		var e = x.Append(10).Sum();

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Sum() + 20;
			var b = x.Sum() + 30;
			var c = x.Sum() + 40;
			var d = x.Sum() + 50;
			var e = x.Sum() + 10;
			
			return a + b + c + d + e;
			""", Unknown),
		Create("return 180;", new[] { 1, 2, 3 }), // a=26, b=36, c=46, d=56, e=16 = 180
		Create("return 150;", new int[] { }), // a=20, b=30, c=40, d=50, e=10 = 150
		Create("return 200;", new[] { 10 }), // a=30, b=40, c=50, d=60, e=20 = 200
	];
}

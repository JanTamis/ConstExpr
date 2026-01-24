namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Cast() optimization - verify that AsEnumerable, ToList, ToArray are skipped
/// </summary>
[InheritsTests]
public class LinqCastOptimizationTests : BaseTest<Func<object[], int>>
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Cast<int>().Sum();
			var b = x.Cast<int>().Sum();
			var c = x.Cast<int>().Sum();
			var d = x.Cast<int>().Sum();
			var e = x.Cast<int>().Sum();
			
			return a + b + c + d + e;
			""", Unknown),
		Create("return 30;", new object[] { 1, 2, 3 }), // sum=6, a=6, b=6, c=6, d=6, e=6 = 30
		Create("return 0;", new object[] { }), // Empty array
		Create("return 50;", new object[] { 10 }), // sum=10, a=10, b=10, c=10, d=10, e=10 = 50
	];
}

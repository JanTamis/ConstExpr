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
		
		// Append followed by Count should be optimized to Length + number of appends
		var f = x.Append(20).Append(30).Append(40).Append(50).Append(10).Count();

		// concat followed by Count should be optimized to Length + number of concatenated elements
		var g = x.Concat([ 1, 2, 3, 4 ]).Count();

		return a + b + c + d + e + f + g;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = Sum_ezMquQ(x) + 20;
			var b = Sum_ezMquQ(x) + 30;
			var c = Sum_ezMquQ(x) + 40;
			var d = Sum_ezMquQ(x) + 50;
			var e = Sum_ezMquQ(x) + 10;
			var f = x.Length + 5;
			var g = x.Length + 4;
			
			return a + b + c + d + e + f + g;
			""", Unknown),
		Create("return 180;", new[] { 1, 2, 3 }), // a=26, b=36, c=46, d=56, e=16 = 180
		Create("return 150;", new int[] { }), // a=20, b=30, c=40, d=50, e=10 = 150
		Create("return 200;", new[] { 10 }), // a=30, b=40, c=50, d=60, e=20 = 200
	];
}

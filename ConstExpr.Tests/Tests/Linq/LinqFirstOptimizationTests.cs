namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for First() optimization - verify that unnecessary operations before First() are removed
/// </summary>
[InheritsTests]
public class LinqFirstOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).First() => First(predicate)
		var a = x.Where(v => v > 3).First();

		// AsEnumerable().First() => First()
		var b = x.AsEnumerable().First();

		// ToList().First() => First()
		var c = x.ToList().First();

		// ToArray().First() => First()
		var d = x.ToArray().First();

		// AsEnumerable().Where().First() => First(predicate)
		var e = x.AsEnumerable().Where(v => v > 2).First();

		// ToList().Where().First() => First(predicate)
		var f = x.ToList().Where(v => v < 5).First();

		// Complex: AsEnumerable().ToList().Where().First() => First(predicate)
		var g = x.AsEnumerable().ToList().Where(v => v == 3).First();

		// Reverse().First() => Last()
		var h = x.Reverse().First();

		// Order().First() => Min()
		var i = x.Order().First();

		// OrderDescending().First() => Max()
		var j = x.OrderDescending().First();

		// Array direct indexing: x.First() => x[0]
		var k = x.First();

		var l = x.Where(v => v > 0).Select(s => s * 2).First();

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.First(v => v > 3);
			var b = x[0];
			var c = x[0];
			var d = x[0];
			var e = x.First(v => v > 2);
			var f = x.First(v => v < 5);
			var g = x.First(v => v == 3);
			var h = x[^1];
			var i = x.Min();
			var j = x.Max();
			var k = x[0];
			var l = x.First(v => v > 0) << 1;
			
			return a + b + c + d + e + f + g + h + i + j + k + l;
			""", Unknown),
		Create("return 28;", new[] { 1, 2, 3, 4, 5 }),
	];
}
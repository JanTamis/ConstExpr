namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Last() optimization - verify that unnecessary operations before Last() are removed
/// </summary>
[InheritsTests]
public class LinqLastOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).Last() => Last(predicate)
		var a = x.Where(v => v > 3).Last();

		// AsEnumerable().Last() => Last()
		var b = x.AsEnumerable().Last();

		// ToList().Last() => Last()
		var c = x.ToList().Last();

		// ToArray().Last() => Last()
		var d = x.ToArray().Last();

		// AsEnumerable().Where().Last() => Last(predicate)
		var e = x.AsEnumerable().Where(v => v > 2).Last();

		// ToList().Where().Last() => Last(predicate)
		var f = x.ToList().Where(v => v < 5).Last();

		// Complex: AsEnumerable().ToList().Where().Last() => Last(predicate)
		var g = x.AsEnumerable().ToList().Where(v => v == 3).Last();

		// Reverse().Last() => First()
		var h = x.Reverse().Last();

		// Order().Last() => Max()
		var i = x.Order().Last();

		// OrderDescending().Last() => Min()
		var j = x.OrderDescending().Last();

		// Array direct indexing: x.Last() => x[^1]
		var k = x.Last();

		// x.Select(s => s * 2).Last() => x[^1] << 1
		var l = x.Where(v => v > 0).Select(s => s * 2).Last();

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Last(v => v > 3);
			var b = x[^1];
			var c = x[^1];
			var d = x[^1];
			var e = x.Last(v => v > 2);
			var f = x.Last(v => v < 5);
			var g = x.Last(v => v == 3);
			var h = x[0];
			var i = x.Max();
			var j = x.Min();
			var k = x[^1];
			var l = x.Last(v => v > 0) << 1;
			
			return a + b + c + d + e + f + g + h + i + j + k + l;
			""", Unknown),
		Create("return 54;", new[] { 1, 2, 3, 4, 5 }),
	];
}


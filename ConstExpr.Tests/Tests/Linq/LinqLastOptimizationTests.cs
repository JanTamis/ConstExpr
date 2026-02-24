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

		return a + b + c + d + e + f + g + h + i + j + k;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x[^1];
			var b = x[^1];
			var c = x[^1];
			var d = x[^1];
			var e = x[^1];
			var f = x[^1];
			var g = x[^1];
			var h = x[0];
			var i = x.Max();
			var j = x.Min();
			var k = x[^1];
			
			return a + b + c + d + e + f + g + h + i + j + k;
			""", Unknown),
		Create("return 44;", new[] { 1, 2, 3, 4, 5 }),
	];
}


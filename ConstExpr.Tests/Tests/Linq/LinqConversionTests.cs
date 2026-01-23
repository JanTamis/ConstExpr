namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ conversion operations - verify constant folding for ToArray, ToList, etc.
/// </summary>
[InheritsTests]
public class LinqConversionTests : BaseTest<Func<IEnumerable<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// ToArray on IEnumerable
		var a = x.ToArray().Length;

		// ToList on IEnumerable
		var b = x.ToList().Count;

		// Cast that's a no-op
		var c = x.Cast<int>().Count();

		// AsEnumerable (should be no-op)
		var d = x.AsEnumerable().Count();

		// ToHashSet with duplicates
		var e = x.Concat(x).ToHashSet().Count;

		// ToDictionary length
		var f = x.ToDictionary(v => v, v => v * 2).Count;

		// ToLookup count
		var g = x.ToLookup(v => v % 2).Count;

		// OfType filtering on mixed collection
		var h = x.Cast<object>().Concat(new object[] { "test", 3.14 }).OfType<int>().Count();

		return a + b + c + d + e + f + g + h;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.ToArray().Length;
			var b = x.ToList().Count;
			var c = x.Count();
			var d = x.Count();
			var e = x.Concat(x).ToHashSet().Count;
			var f = x.ToDictionary(v => v, v => v * 2).Count;
			var g = x.ToLookup(v => v % 2).Count;
			var h = x.Cast<Object>().Concat(new Object[]
			{
				"test",
				3.14D
			}).OfType<Int32>().Count();

			return a + b + c + d + e + f + g + h;
			""", Unknown),
		Create("return 21;", new[] { 1, 2, 3 }),
	];
}

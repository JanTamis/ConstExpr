namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ element access operations - verify constant folding for First, Last, ElementAt, etc.
/// </summary>
[InheritsTests]
public class LinqElementAccessTests : BaseTest<Func<IEnumerable<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// First and Last on IEnumerable
		var a = x.First();
		var b = x.Last();

		// FirstOrDefault and LastOrDefault
		var c = x.FirstOrDefault();
		var d = x.LastOrDefault();

		// ElementAt with constant index
		var e = x.ElementAt(2);

		// ElementAtOrDefault
		var f = x.ElementAtOrDefault(1);

		// First/Last with predicate
		var g = x.First(v => v > 2);
		var h = x.Last(v => v < 4);

		// Skip to test with single element
		var i = x.Skip(x.Count() - 1).Single();

		// Take to test SingleOrDefault
		var j = x.Take(1).SingleOrDefault();

		return a + b + c + d + e + f + g + h + i + j;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.First();
			var b = x.Last();
			var c = x.FirstOrDefault();
			var d = x.LastOrDefault();
			var e = x.ElementAt(2);
			var f = x.ElementAtOrDefault(1);
			var g = x.First(v => v > 2);
			var h = x.Last(v => v < 4);
			var i = x.Skip(x.Count() - 1).Single();
			var j = x.Take(1).SingleOrDefault();

			return a + b + c + d + e + f + g + h + i + j;
			""", Unknown),
		Create("return 150;", new[] { 10, 20, 30, 40, 50 }),
	];
}

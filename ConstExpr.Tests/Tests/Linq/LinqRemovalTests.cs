namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ function removal - verify that unnecessary LINQ operations are eliminated
/// </summary>
[InheritsTests]
public class LinqRemovalTests : BaseTest<Func<IEnumerable<int>, int>>
{
	public override string TestMethod => GetString((x) =>
	{
		// Where with always-true condition should be removed
		var a = x.Where(v => true).Count();

		// Select that doesn't transform should be optimized
		var b = x.Select(v => v).Count();

		// FirstOrDefault with single element
		var c = x.FirstOrDefault();

		// Chained Where filters
		var d = x.Where(v => v > 0).Where(v => v < 10).Count();

		// Take with length greater than array
		var e = x.Take(10).Count();

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 16;", 2),
		Create("return 17;", 10),
	];
}


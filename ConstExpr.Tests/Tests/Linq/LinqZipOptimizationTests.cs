namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Zip() optimization - verify empty collection handling
/// </summary>
[InheritsTests]
public class LinqZipOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Zip with empty => empty
		var a = x.Zip(Enumerable.Empty<int>()).Count();

		// Empty.Zip(collection) => empty
		var b = Enumerable.Empty<int>().Zip(x).Count();

		var c = x.Zip(x).Count();
		
		var d = x.Zip(x.Where(w => w > 0)).Count();

		return a + b + c + d;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var c = x.Length;
			var d = Int32.Min(x.Length, Count_FDQQ2g(x));
			
			return c + d;
			""", Unknown),
		Create("return 6;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}
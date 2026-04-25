namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for Take() optimization - verify Take(0) returns Empty
/// </summary>
[InheritsTests]
public class LinqTakeOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Take(0) => Enumerable.Empty<T>()
		var a = x.Take(0).Count();

		var b = x.Take(1).AsEnumerable().Take(3).Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var b = Int32.Min(1, x.Length);

			return b;
			"""),
		Create("return 1;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}
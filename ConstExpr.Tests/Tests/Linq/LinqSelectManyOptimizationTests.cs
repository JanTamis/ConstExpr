namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for SelectMany() optimization - verify empty collection handling
/// </summary>
[InheritsTests]
public class LinqSelectManyOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Enumerable.Empty<T>().SelectMany(selector) => Enumerable.Empty<TResult>()
		var a = Enumerable.Empty<int[]>().SelectMany(arr => arr).Count();

		// SelectMany always returning empty => Empty
		var b = x.SelectMany(v => Enumerable.Empty<int>()).Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return 0;", Unknown),
		Create("return 0;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}


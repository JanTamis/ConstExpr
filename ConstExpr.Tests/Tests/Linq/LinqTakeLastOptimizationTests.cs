namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for TakeLast() optimization - verify TakeLast(0) returns Empty
/// </summary>
[InheritsTests]
public class LinqTakeLastOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// TakeLast(0) => Enumerable.Empty<T>()
		var a = x.TakeLast(0).Count();

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return 0;", Unknown),
		Create("return 0;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}


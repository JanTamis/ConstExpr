namespace ConstExpr.Tests.Linq;

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

		var b = x.TakeLast(1).TakeLast(5).Count();

		return a + b;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var b = Count_XgBl1Q(x);
			
			return b;
			"""),
		Create("return 1;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}
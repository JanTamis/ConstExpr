namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Select() optimization - verify identity lambdas, cast optimizations, and lambda fusion
/// </summary>
[InheritsTests]
public class LinqSelectOptimizationTests : BaseTest<Func<IEnumerable<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Identity lambda: Select(y => y) => remove Select
		var a = x.Select(y => y).Sum();

		// Cast lambda followed by Sum: Select(y => (int?)y).Sum() => Sum(y => (int?)y)
		var b = x.Select(y => (int?)y).Sum() ?? 0;

		// Lambda fusion: Select(y => y * 2).Select(z => z + 1) => Select(y => y * 2 + 1)
		var c = x.Select(y => y * 2).Select(z => z + 1).Sum();

		return a + b + c;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		// Create("""
		// 	var a = x.Sum();
		// 	var b = x.Cast<int?>().Sum() ?? 0;
		// 	var c = x.Sum(y => (y << 1) + 1);
		// 	
		// 	return a + b + c;
		// 	""", Unknown),
		// Create("return 27;", new[] { 1, 2, 3 }), // a=6, b=6, c=(3)+(5)+(7)=15 = 27
		Create("return 0;", Enumerable.Empty<int>()), // a=0, b=0, c=0 = 0
		Create("return 21;", new[] { 5 }), // a=5, b=5, c=11 = 21
	];
}
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

		// Cast lambda: Select(y => y as double) => Cast<double>()
		var b = x.Select(y => (int?)y).Sum() ?? 0;

		// Lambda fusion: Select(y => y * 2).Select(z => z + 1) => Select(y => y * 2 + 1)
		var c = x.Select(y => y * 2).Select(z => z + 1).Sum();

		return a + b + c;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Sum();
			var b = x.Cast<int?>().Sum() ?? 0;
			var c = x.Select(y => y << 1 + 1).Sum();
			
			return a + b + c;
			""", Unknown),
		Create("return 27;", new[] { 1, 2, 3 }), // a=6, b=6, c=(3)+(5)+(7)=15 = 27
		Create("return 6;", Enumerable.Empty<int>()), // a=0, b=6, c=0 = 6
		Create("return 17;", new[] { 5 }), // a=5, b=6, c=11 = 22
	];
}
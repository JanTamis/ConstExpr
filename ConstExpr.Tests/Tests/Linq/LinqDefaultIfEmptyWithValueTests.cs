namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for DefaultIfEmpty() with custom default value
/// </summary>
[InheritsTests]
public class  LinqDefaultIfEmptyWithValueTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// DefaultIfEmpty with custom value
		var a = x.DefaultIfEmpty(42).First();
		
		// Distinct().DefaultIfEmpty(value) => DefaultIfEmpty(value)
		var b = x.Distinct().DefaultIfEmpty(99).First();

		// OrderBy().DefaultIfEmpty(value) => DefaultIfEmpty(value)
		var c = x.OrderBy(v => v).DefaultIfEmpty(77).First();

		// DefaultIfEmpty(10).DefaultIfEmpty(20) => DefaultIfEmpty(10) (inner/first value wins)
		var d = x.DefaultIfEmpty(10).DefaultIfEmpty(20).First();

		return a + b + c + d;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (x.Length > 0 ? x[0] : 42) + (x.Length > 0 ? x[0] : 99) + First_mA5pFw(x) + (x.Length > 0 ? x[0] : 10);"),
		Create("return 4;", new[] { 1 }), // Non-empty: returns first element (1) four times = 1+1+1+1 = 4
		Create("return 228;", new int[] { }), // Empty: returns default values 42+99+77+20 = 238
	];
}
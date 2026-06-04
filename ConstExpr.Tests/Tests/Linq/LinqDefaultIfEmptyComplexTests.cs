using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for DefaultIfEmpty() with complex scenarios
/// </summary>
[InheritsTests]
public class LinqDefaultIfEmptyComplexTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Multiple chained operations before DefaultIfEmpty
		var a = x.Where(v => v > 0).Distinct().OrderBy(v => v).DefaultIfEmpty(50).Sum();

		// DefaultIfEmpty after Select (Select can create empty collection)
		var b = x.Where(v => v > 100).Select(v => v * 2).DefaultIfEmpty(25).Sum();

		// Nested DefaultIfEmpty with different values
		var c = x.DefaultIfEmpty(10).DefaultIfEmpty(20).DefaultIfEmpty(30).First();
		var d = x.DefaultIfEmpty(10).DefaultIfEmpty(20).DefaultIfEmpty(30).FirstOrDefault();

		var e = x.DefaultIfEmpty(10).DefaultIfEmpty(20).DefaultIfEmpty(30).Last();
		var f = x.DefaultIfEmpty(10).DefaultIfEmpty(20).DefaultIfEmpty(30).LastOrDefault();
		return a + b + c + d + e + f;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (x.Length > 0 ? x[0] * 2 : 20) + (x.Length > 0 ? x[^1] * 2 : 20) + Sum_HI9NYg(x) + Sum_swQo7g(x);"),
		Create(_ => 52, [ new[] { 1, 2, 3, 4, 5 } ]), // a=15 (sum 1-5), b=25 (empty→default), c=1, d=1 (first), e=5, f=5 (last) = 52
		Create(_ => 115, [ System.Array.Empty<int>() ]), // a=50 (empty→default), b=25 (empty→default), c=d=e=f=10 (empty→innermost default) = 115
	];
}
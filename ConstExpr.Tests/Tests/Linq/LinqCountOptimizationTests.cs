using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Count() optimization - verify that unnecessary operations before Count() are removed
/// </summary>
[InheritsTests]
public class LinqCountOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).Count() => Count(predicate)
		var a = x.Where(v => v > 3).Count();
		
		// OrderBy(...).Count() => Count()
		var b = x.OrderBy(v => v).Count();
		
		// OrderByDescending(...).Count() => Count()
		var c = x.OrderByDescending(v => v).Count();
		
		// Reverse().Count() => Count()
		var d = x.Reverse().Count();
		
		// AsEnumerable().Count() => Count()
		var e = x.AsEnumerable().Count();
		
		// OrderBy().ThenBy().Count() => Count()
		var f = x.OrderBy(v => v).ThenBy(v => v * 2).Count();
		
		// OrderBy().Where().Count() => Count(predicate)
		var g = x.OrderBy(v => v).Where(v => v > 2).Count();

		// Complex: OrderBy().ThenBy().Reverse().Where().Count() => Count(predicate)
		var h = x.OrderBy(v => v).ThenBy(v => v * 2).Reverse().Where(v => v < 5).Count();

		// Distinct should NOT be optimized (reduces count!)
		var i = x.Distinct().Concat(x).Concat(x).Count();
		
		// Select should be optimized away
		var j = x.Select(v => v * 2).Count();
		
		// Multiple chained Where statements should be combined
		var k = x.Where(v => v > 2).Where(v => v < 10).Count();
		
		// Three chained Where statements
		var l = x.Where(v => v > 1).Where(v => v < 8).Where(v => v % 2 == 0).Count();
		
		// Where with operations that don't affect count
		var m = x.OrderBy(v => v).Where(v => v > 2).Where(v => v < 10).Count();
		
		// Complex chain with multiple Where statements
		var n = x.Where(v => v > 1).OrderBy(v => v).Where(v => v < 8).Reverse().Where(v => v % 2 == 0).Count();
		
		var o = x.GroupBy(v => v % 3).Count();

		return a + b + c + d + e + f + g + h + i + j + k + l + m + n + o;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = Count_YEOoVA(x);
			var b = x.Length;
			var c = x.Length;
			var d = x.Length;
			var e = x.Length;
			var f = x.Length;
			var g = Count_ZKr__A(x);
			var h = Count_5UQwMQ(x);
			var i = Count_4OhS1w(x) + x.Length + x.Length;
			var j = x.Length;
			var k = Count_SzY_Mw(x);
			var l = Count_lj8aiA(x);
			var m = Count_SzY_Mw(x);
			var n = Count_lj8aiA(x);
			var o = Count_T6dSbQ(x);
			
			return a + b + c + d + e + f + g + h + i + j + k + l + m + n + o;
			""", Unknown),
		Create("return 67;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }),
	];
}

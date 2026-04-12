namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for FirstOrDefault() optimization - verify that unnecessary operations before FirstOrDefault() are removed
/// </summary>
[InheritsTests]
public class LinqFirstOrDefaultOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).FirstOrDefault() => FirstOrDefault(predicate)
		var a = x.Where(v => v > 3).FirstOrDefault();

		// AsEnumerable().FirstOrDefault() => FirstOrDefault()
		var b = x.AsEnumerable().FirstOrDefault();

		// ToList().FirstOrDefault() => FirstOrDefault()
		var c = x.ToList().FirstOrDefault();

		// ToArray().FirstOrDefault() => FirstOrDefault()
		var d = x.ToArray().FirstOrDefault();

		// AsEnumerable().Where().FirstOrDefault() => FirstOrDefault(predicate)
		var e = x.AsEnumerable().Where(v => v > 2).FirstOrDefault();

		// ToList().Where().FirstOrDefault() => FirstOrDefault(predicate)
		var f = x.ToList().Where(v => v < 5).FirstOrDefault();

		// Complex: AsEnumerable().ToList().Where().FirstOrDefault() => FirstOrDefault(predicate)
		var g = x.AsEnumerable().ToList().Where(v => v == 3).FirstOrDefault();

		// OrderBy should NOT be optimized (changes which element is first!)
		var h = x.OrderBy(v => v).FirstOrDefault();

		// Reverse should NOT be optimized (changes which element is first!)
		var i = x.Reverse().FirstOrDefault();

		// Distinct should NOT be optimized (first element might be duplicate!)
		var j = x.Distinct().FirstOrDefault();

		// Array conditional: x.FirstOrDefault() => x.Length > 0 ? x[0] : default
		var k = x.FirstOrDefault();

		var l = x.Where(v => v > 0).Select(s => s * 2).FirstOrDefault();

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = Array.Find(x, v => v > 3);
			var b = x.Length > 0 ? x[0] : 0;
			var c = x.Length > 0 ? x[0] : 0;
			var d = x.Length > 0 ? x[0] : 0;
			var e = Array.Find(x, v => v > 2);
			var f = Array.Find(x, v => v < 5);
			var g = Array.Find(x, v => v == 3);
			var h = Min_BeESfw(x);
			var i = x.Length > 0 ? x[^1] : 0;
			var j = x.Length > 0 ? x[0] : 0;
			var k = x.Length > 0 ? x[0] : 0;
			var l = Array.Find(x, v => v > 0) << 1;
			
			return a + b + c + d + e + f + g + h + i + j + k + l;
			""", Unknown),
		Create("return 24;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }),
	];
}
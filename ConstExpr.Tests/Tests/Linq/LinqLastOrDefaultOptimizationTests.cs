namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LastOrDefault() optimization - verify that unnecessary operations before LastOrDefault() are removed
/// </summary>
[InheritsTests]
public class LinqLastOrDefaultOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).LastOrDefault() => LastOrDefault(predicate)
		var a = x.Where(v => v > 3).LastOrDefault();

		// AsEnumerable().LastOrDefault() => LastOrDefault()
		var b = x.AsEnumerable().LastOrDefault();

		// ToList().LastOrDefault() => LastOrDefault()
		var c = x.ToList().LastOrDefault();

		// ToArray().LastOrDefault() => LastOrDefault()
		var d = x.ToArray().LastOrDefault();

		// AsEnumerable().Where().LastOrDefault() => LastOrDefault(predicate)
		var e = x.AsEnumerable().Where(v => v > 2).LastOrDefault();

		// ToList().Where().LastOrDefault() => LastOrDefault(predicate)
		var f = x.ToList().Where(v => v < 5).LastOrDefault();

		// Complex: AsEnumerable().ToList().Where().LastOrDefault() => LastOrDefault(predicate)
		var g = x.AsEnumerable().ToList().Where(v => v == 3).LastOrDefault();

		// OrderBy().LastOrDefault() => Max() (sorted ascending, last = max)
		var h = x.OrderBy(v => v).LastOrDefault();

		// Reverse().LastOrDefault() => FirstOrDefault() (reversed, last = original first)
		var i = x.Reverse().LastOrDefault();

		// Array conditional: x.LastOrDefault() => x.Length > 0 ? x[^1] : default
		var j = x.LastOrDefault();

		return a + b + c + d + e + f + g + h + i + j;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.LastOrDefault(v => v > 3);
			var b = x.Length > 0 ? x[^1] : 0;
			var c = x.Length > 0 ? x[^1] : 0;
			var d = x.Length > 0 ? x[^1] : 0;
			var e = x.LastOrDefault(v => v > 2);
			var f = x.LastOrDefault(v => v < 5);
			var g = x.LastOrDefault(v => v == 3);
			var h = x.Max();
			var i = x.Length > 0 ? x[0] : 0;
			var j = x.Length > 0 ? x[^1] : 0;

			return a + b + c + d + e + f + g + h + i + j;
			""", Unknown),
		Create("return 43;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }),
	];
}





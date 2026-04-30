using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for LastOrDefault() optimization - verify that unnecessary operations before LastOrDefault() are removed
/// </summary>
[InheritsTests]
public class LinqLastOrDefaultOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
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
		
		var k = x.Where(v => v > 0).Select(s => s * 2).LastOrDefault();

		return a + b + c + d + e + f + g + h + i + j + k;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Array.FindLast(x, v => v > 0) * 2 + (x.Length > 0 ? x[^1] * 4 : 0) + Array.FindLast(x, v => v > 3) + Array.FindLast(x, v => v > 2) + Array.FindLast(x, v => v < 5) + Array.FindLast(x, v => v == 3) + Max_uzcZ3A(x) + (x.Length > 0 ? x[0] : 0);"),
		Create("return 53;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }),
	];
}





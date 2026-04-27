using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for First() optimization - verify that unnecessary operations before First() are removed
/// </summary>
[InheritsTests]
public class LinqFirstOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).First() => First(predicate)
		var a = x.Where(v => v > 3).First();

		// AsEnumerable().First() => First()
		var b = x.AsEnumerable().First();

		// ToList().First() => First()
		var c = x.ToList().First();

		// ToArray().First() => First()
		var d = x.ToArray().First();

		// AsEnumerable().Where().First() => First(predicate)
		var e = x.AsEnumerable().Where(v => v > 2).First();

		// ToList().Where().First() => First(predicate)
		var f = x.ToList().Where(v => v < 5).First();

		// Complex: AsEnumerable().ToList().Where().First() => First(predicate)
		var g = x.AsEnumerable().ToList().Where(v => v == 3).First();

		// Reverse().First() => Last()
		var h = x.Reverse().First();

		// Order().First() => Min()
		var i = x.Order().First();

		// OrderDescending().First() => Max()
		var j = x.OrderDescending().First();

		// Array direct indexing: x.First() => x[0]
		var k = x.First();

		var l = x.Where(v => v > 0).Select(s => s * 2).First();

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x[0] * 4 + First_zVpC_g(x) * 2 + First_x5lKxQ(x) + First_O1a9Fw(x) + First_NyySEw(x) + First_BgmaKg(x) + x[^1] + Min_zgmZ3g(x) + Max_uzcZ3A(x);"),
		Create("return 28;", new[] { 1, 2, 3, 4, 5 }),
	];
}
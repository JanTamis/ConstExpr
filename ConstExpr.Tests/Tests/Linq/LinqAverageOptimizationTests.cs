using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for Average() optimization - verify that AsEnumerable, ToList, ToArray are skipped
/// </summary>
[InheritsTests]
public class LinqAverageOptimizationTests() : BaseTest<Func<int[], double>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// AsEnumerable().Average() => collection.Average() (skip AsEnumerable)
		var a = x.AsEnumerable().Average();

		// ToList().Average() => collection.Average() (skip ToList)
		var b = x.ToList().Average();

		// ToArray().Average() => collection.Average() (skip ToArray)
		var c = x.ToArray().Average();

		// Multiple skip operations
		var d = x.AsEnumerable().ToList().Average();

		// Regular Average (should not be optimized)
		var e = x.Average(v => v);

		// Average with selector - AsEnumerable
		var f = x.AsEnumerable().Average(v => v * 2);

		// Average with selector - ToList
		var g = x.ToList().Average(v => v * 3);

		var h = x.Select(s => s * 2).Average();

		return a + b + c + d + e + f + g + h;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Average_YxQu4A(x) * 5D + Average_5JubKw(x) + Average_pmtLFg(x) + Average_zm9RrA(x);"),
		Create("return 24D;", new[] { 1, 2, 3 }),
		Create("throw new InvalidOperationException(\"Sequence contains no elements\");", new int[] { }),
		Create("return 120D;", new[] { 10 })
	];
}
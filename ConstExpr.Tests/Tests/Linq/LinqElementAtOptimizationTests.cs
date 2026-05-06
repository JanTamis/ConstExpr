using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for ElementAt() optimization - verify that unnecessary operations before ElementAt() are removed
/// and that ElementAt is optimized to direct array/list indexing when possible
/// Note: ElementAt(0) is optimized to First() which is more idiomatic
/// </summary>
[InheritsTests]
public class LinqElementAtOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Simple ElementAt(0) - should become First()
		var a = x.ElementAt(0);

		// AsEnumerable().ElementAt() => ElementAt() => array indexing
		var b = x.AsEnumerable().ElementAt(1);

		// ToList().ElementAt() => ElementAt() => array indexing
		var c = x.ToList().ElementAt(2);

		// ToArray().ElementAt(0) => First()
		var d = x.ToArray().ElementAt(0);

		// AsEnumerable().ToList().ElementAt() => ElementAt() => array indexing
		var e = x.AsEnumerable().ToList().ElementAt(1);

		// Complex chain: AsEnumerable().ToArray().ElementAt() => array indexing
		var f = x.AsEnumerable().ToArray().ElementAt(2);

		// ElementAt with different index
		var g = x.ElementAt(3);

		return a + b + c + d + e + f + g;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x[0] * 2 + x[1] * 2 + x[2] * 2 + x[3];"),
		Create("return 16;", new[] { 1, 2, 3, 4, 5 }), // 1 + 2 + 3 + 1 + 2 + 3 + 4 = 16
		Create("return 0;", new[] { 0, 0, 0, 0, 0 }),
	];
}
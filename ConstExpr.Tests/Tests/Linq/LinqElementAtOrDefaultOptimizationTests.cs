using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for ElementAtOrDefault() optimization - verify that unnecessary operations before ElementAtOrDefault() are removed
/// Note: ElementAtOrDefault cannot be optimized to direct indexing because it returns default instead of throwing
/// </summary>
[InheritsTests]
public class LinqElementAtOrDefaultOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Simple ElementAtOrDefault
		var a = x.ElementAtOrDefault(0);

		// AsEnumerable().ElementAtOrDefault() => ElementAtOrDefault()
		var b = x.AsEnumerable().ElementAtOrDefault(1);

		// ToList().ElementAtOrDefault() => ElementAtOrDefault()
		var c = x.ToList().ElementAtOrDefault(2);

		// ToArray().ElementAtOrDefault() => ElementAtOrDefault()
		var d = x.ToArray().ElementAtOrDefault(0);

		// AsEnumerable().ToList().ElementAtOrDefault() => ElementAtOrDefault()
		var e = x.AsEnumerable().ToList().ElementAtOrDefault(1);

		// Complex chain: AsEnumerable().ToArray().ElementAtOrDefault() => ElementAtOrDefault()
		var f = x.AsEnumerable().ToArray().ElementAtOrDefault(2);

		// ElementAtOrDefault with index out of bounds (should return 0 for int)
		var g = x.ElementAtOrDefault(10);

		// ElementAtOrDefault with valid index
		var h = x.ElementAtOrDefault(3);

		return a + b + c + d + e + f + g + h;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x.Length > 0 ? x[0] * 2 : 0) + (x.Length > 1 ? x[1] * 2 : 0) + (x.Length > 2 ? x[2] * 2 : 0) + (x.Length > 10 ? x[10] : 0) + (x.Length > 3 ? x[3] : 0)),
		Create(_ => 16, [ new[] { 1, 2, 3, 4, 5 } ]), // 1 + 2 + 3 + 1 + 2 + 3 + 0 + 4 = 16
		Create(_ => 0, [ System.Array.Empty<int>() ]), // All return 0 (default)
		Create(_ => 0, [ new[] { 0, 0, 0, 0, 0 } ]),
	];
}
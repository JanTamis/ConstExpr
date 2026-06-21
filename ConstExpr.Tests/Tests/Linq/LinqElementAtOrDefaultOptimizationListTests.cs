using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for ElementAtOrDefault() optimization on List
/// </summary>
[InheritsTests]
public class LinqElementAtOrDefaultOptimizationListTests() : BaseTest<Func<List<int>, int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Simple ElementAtOrDefault on List
		var a = x.ElementAtOrDefault(0);

		// AsEnumerable().ElementAtOrDefault() => ElementAtOrDefault()
		var b = x.AsEnumerable().ElementAtOrDefault(1);

		// ToArray().ElementAtOrDefault() => ElementAtOrDefault()
		var c = x.ToArray().ElementAtOrDefault(0);

		// ToList().ElementAtOrDefault() => ElementAtOrDefault()
		var d = x.ToList().ElementAtOrDefault(1);

		// Out of bounds
		var e = x.ElementAtOrDefault(10);

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x.Count > 0 ? x[0] * 2 : 0) + (x.Count > 1 ? x[1] * 2 : 0) + (x.Count > 10 ? x[10] : 0)),
		Create(_ => 6, [ new List<int> { 1, 2, 3, 4, 5 } ]), // 1 + 2 + 1 + 2 + 0 = 6
		Create(_ => 0, [ new List<int>() ]) // All return 0 (default)
	];
}
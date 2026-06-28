using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Contains() optimization on List - verify that Contains is optimized for List type
/// </summary>
[InheritsTests]
public class LinqContainsOptimizationListTests() : BaseTest<Func<List<int>, int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Simple Contains
		var a = x.Contains(3) ? 1 : 0;

		// Distinct().Contains() => Contains()
		var b = x.Distinct().Contains(3) ? 1 : 0;

		// OrderBy(...).Contains() => Contains()
		var c = x.OrderBy(v => v).Contains(3) ? 1 : 0;

		// Reverse().Contains() => Contains()
		var d = x.AsEnumerable().Reverse().Contains(3) ? 1 : 0;

		// Select(...).Contains() => Exists(...)
		var e = x.Select(v => v * 2).Contains(6) ? 1 : 0;

		// Where(...).Contains() => Exists(...)
		var f = x.Where(v => v > 2).Contains(3) ? 1 : 0;

		// Contains with value not present
		var g = x.Contains(100) ? 1 : 0;

		return a + b + c + d + e + f + g;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (Contains__KFndQ(CollectionsMarshal.AsSpan(x)) ? 6 : 0) + (Contains_H_mKqw(CollectionsMarshal.AsSpan(x)) ? 1 : 0);", Unknown),
		Create(_ => 6, [ new List<int> { 1, 2, 3, 4, 5 } ]),
		Create(_ => 0, [ new List<int>() ]),
		Create(_ => 0, [ new List<int> { 1, 2, 4, 5, 6 } ]) // No 3, all tests fail
	];
}
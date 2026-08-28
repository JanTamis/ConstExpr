namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Contains() optimization on List - verify that Contains is optimized for List type
/// </summary>
[InheritsTests]
public class LinqContainsOptimizationListTests : BaseTestWithRandomValues<Func<List<int>, int>>
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

		// Select(...).Contains() => Exists(...); v * 2 == 6 does NOT fold further to v == 3 — see
		// LinqContainsOptimizationTests for why multiply-by-even-c can't isolate safely.
		var e = x.Select(v => v * 2).Contains(6) ? 1 : 0;

		// Where(...).Contains() => Exists(...)
		var f = x.Where(v => v > 2).Contains(3) ? 1 : 0;

		// Contains with value not present
		var g = x.Contains(100) ? 1 : 0;

		return a + b + c + d + e + f + g;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var collectionsMarshalAsSpan = CollectionsMarshal.AsSpan(x);

			return Unsafe.BitCast<bool, byte>(VectorOperations.Any<int, ContainsOperatorWQ96KQ>(collectionsMarshalAsSpan)) * 5 + Unsafe.BitCast<bool, byte>(x.Exists(v => v << 1 == 6)) + Unsafe.BitCast<bool, byte>(VectorOperations.Any<int, ContainsOperatorExtsXQ>(collectionsMarshalAsSpan));
			""", Unknown),
	];
}
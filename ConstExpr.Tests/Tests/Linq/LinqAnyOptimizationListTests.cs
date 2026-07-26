namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Any() optimization on List - verify that List.Where().Any() is optimized to List.Exists()
/// </summary>
[InheritsTests]
public class LinqAnyOptimizationListTests : BaseTest<Func<List<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// List.Where(...).Any() => List.Exists(...)
		var a = x.Where(v => v > 3).Any() ? 1 : 0;

		// List.Select(...).Any() => List.Any()
		var b = x.Select(v => v * 2).Any() ? 1 : 0;

		// List.OrderBy(...).Any() => List.Any()
		var c = x.OrderBy(v => v).Any() ? 1 : 0;

		// List.Where filters everything out => List.Exists(...)
		var d = x.Where(v => v > 100).Any() ? 1 : 0;

		// Should be optimized to Contains
		var e = x.Any(v => v == 2) ? 1 : 0;

		// Direct Any() on list => x.Count > 0
		var f = x.Any() ? 1 : 0;

		return a + b + c + d + e + f;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var asSpanVal = CollectionsMarshal.AsSpan(x);

			return Unsafe.BitCast<bool, byte>(x.Count > 0) * 3 + Unsafe.BitCast<bool, byte>(VectorOperations.Any<int, Operator2sjEFw>(asSpanVal)) + Unsafe.BitCast<bool, byte>(VectorOperations.Any<int, OperatorkNzSYw>(asSpanVal)) + Unsafe.BitCast<bool, byte>(Contains_rph_Xw(asSpanVal));
			""", Unknown),
		Create(_ => 5, [ new List<int> { 1, 2, 3, 4, 5 } ]),
		Create(_ => 0, [ new List<int>() ])
	];
}
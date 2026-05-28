using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for Any() optimization on List - verify that List.Where().Any() is optimized to List.Exists()
/// </summary>
[InheritsTests]
public class LinqAnyOptimizationListTests() : BaseTest<Func<List<int>, int>>(FastMathFlags.AssociativeMath)
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
		Create("return (x.Count > 0 ? 3 : 0) + (Any_pfIHsA(CollectionsMarshal.AsSpan(x)) ? 1 : 0) + (Any_7KBy_w(CollectionsMarshal.AsSpan(x)) ? 1 : 0) + (Contains_Xl5chw(CollectionsMarshal.AsSpan(x)) ? 1 : 0);", Unknown),
		Create("return 5;", new List<int> { 1, 2, 3, 4, 5 }),
		Create("return 0;", new List<int>()),
	];
}
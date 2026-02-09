using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Where() optimization - verify constant folding and combining of Where clauses
/// </summary>
[InheritsTests]
public class LinqWhereOptimizationTests() : BaseTest<Func<int[], int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Where(v => true) - should be removed entirely
		var a = x.Where(v => true).Count();

		// Where(v => false) - should be replaced with Empty
		var b = x.Where(v => false).Count();

		// Consecutive Where calls with same parameter - should combine with &&
		var c = x.Where(v => v > 1).Where(v => v < 5).Count();

		// Consecutive Where calls with different parameters - should still combine
		var d = x.Where(v => v > 0).Where(r => r < 10).Count();

		// Multiple consecutive Where calls - should combine all
		var e = x.Where(v => v > 0).Where(v => v < 10).Where(v => v % 2 == 0).Count();

		// Where(v => true) in chain - should be removed
		var f = x.Where(v => true).Where(v => v > 3).Count();

		// Where(v => false) in chain - result should be empty
		var g = x.Where(v => v > 1).Where(v => false).Count();

		// Complex predicates
		var h = x.Where(v => v > 0 && v < 100).Where(v => v % 2 == 0).Count();

		return a + b + c + d + e + f + g + h;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Length;
			var c = x.Count(v => (uint)v - 1 < 4U);
			var d = x.Count(v => (uint)v < 10U);
			var e = x.Count(v => (uint)v < 10U && Int32.IsEvenInteger(v));
			var f = x.Count(v => v > 3);
			var h = x.Count(v => (uint)v < 100U && Int32.IsEvenInteger(v));
			
			return a + c + d + e + f + h;
			""", Unknown),
		Create("return 17;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", new int[] { }),
		Create("return 30;", new[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 }),
	];
}

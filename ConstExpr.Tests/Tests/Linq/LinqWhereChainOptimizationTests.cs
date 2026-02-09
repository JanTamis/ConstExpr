using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Where() optimization with multiple chained Where statements
/// </summary>
[InheritsTests]
public class LinqWhereChainOptimizationTests() : BaseTest<Func<int[], IEnumerable<int>>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Two Where statements
		var a = x.Where(v => v > 2).Where(v => v < 10);

		// Three Where statements
		var b = x.Where(v => v > 1).Where(v => v < 8).Where(v => v % 2 == 0);

		// Four Where statements
		var c = x.Where(v => v > 0).Where(v => v < 100).Where(v => v % 3 == 0).Where(v => v < 50);

		// Where with different parameter names
		var d = x.Where(p => p > 5).Where(q => q < 15);

		// Where(true) should be removed in chain
		var e = x.Where(v => true).Where(v => v > 3).Where(v => v < 7);

		// Where(false) should make entire chain empty
		var f = x.Where(v => v > 1).Where(v => false).Where(v => v < 10);

		return a.Concat(b).Concat(c).Concat(d).Concat(e).Concat(f);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Where(v => (uint)v - 2 < 8U);
			var b = x.Where(v => (uint)v - 1 < 7U && Int32.IsEvenInteger(v));
			var c = x.Where(v => (uint)v < 100U && v % 3 == 0 && v < 50);
			var d = x.Where(p => (uint)p - 5 < 10U);
			var e = x.Where(v => (uint)v - 3 < 4U);
			var f = Enumerable.Empty<int>();
			
			return a.Concat(b).Concat(c).Concat(d).Concat(e).Concat(f);
			""", Unknown),
	];
}






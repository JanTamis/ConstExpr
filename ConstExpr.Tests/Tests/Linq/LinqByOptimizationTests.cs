using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

[InheritsTests]
public class LinqByOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x =>
	{
		var a = x.DistinctBy(v => v % 2).Count();
		var b = x.ExceptBy([ 2, 4, 6 ], v => v).Count();
		var c = x.IntersectBy([ 2, 4, 6 ], v => v).Count();
		var d = x.UnionBy([ 2, 4, 6 ], v => v).Count();

		return a + b + c + d;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Count_fDCnXg(x) + Count_A_x9WQ(x) + Count_GdwhZA(x) + Count_0yIQSg(x);", Unknown),
		Create("return 11;", new[] { 1, 2, 2, 3, 4 }),
		Create("return 3;", new int[] { }),
	];
}
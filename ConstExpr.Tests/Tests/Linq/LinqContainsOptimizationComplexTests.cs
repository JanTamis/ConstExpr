using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for Contains() optimization with complex lambda expressions
/// </summary>
[InheritsTests]
public class LinqContainsOptimizationComplexTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Multiple chained operations before Contains
		var a = x.Where(v => v > 0).Distinct().OrderBy(v => v).Contains(5) ? 1 : 0;

		// Select with more complex expression
		var b = x.Select(v => v + 10).Concat(x).Contains(15) ? 1 : 0;

		// Where with complex predicate
		var c = x.Where(v => v % 2 == 0).Contains(4) ? 1 : 0;

		// Nested operations
		var d = x.Distinct().Where(v => v < 10).Contains(5) ? 1 : 0;

		return a + b + c + d;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (Contains_lhimyw(x) ? 2 : 0) + (Contains_lhimyw(x) || Contains_slHxSg(x) ? 1 : 0) + (Contains_V_sWdg(x) ? 1 : 0);", Unknown),
		Create("return 4;", new[] { 1, 2, 3, 4, 5, 6, 7, 8 }),
		Create("return 0;", new int[] { }),
		Create("return 3;", new[] { 5, 10, 15 }), // a=1 (5>0 && 5==5), b=1 (5+10==15), c=0 (no 4), d=1 (5<10 && 5==5)
	];
}
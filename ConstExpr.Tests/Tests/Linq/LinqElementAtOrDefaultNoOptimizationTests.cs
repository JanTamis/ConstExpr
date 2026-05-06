using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests that operations which affect element positions are NOT optimized for ElementAtOrDefault
/// </summary>
[InheritsTests]
public class LinqElementAtOrDefaultNoOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// OrderBy should NOT be optimized (changes element positions!)
		var a = x.OrderBy(v => v).ElementAtOrDefault(0);

		// OrderByDescending should NOT be optimized
		var b = x.OrderByDescending(v => v).ElementAtOrDefault(0);

		// Reverse should NOT be optimized
		var c = x.Reverse().ElementAtOrDefault(0);

		// Where should NOT be optimized (changes collection size and indices)
		var d = x.Where(v => v > 2).ElementAtOrDefault(0);

		// Select should NOT be optimized (transforms elements)
		var e = x.Select(v => v * 2).ElementAtOrDefault(0);

		// Distinct should NOT be optimized (removes duplicates, changes indices)
		var f = x.Distinct().ElementAtOrDefault(0);

		return a + b + c + d + e + f;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (x.Length > 0 ? x[0] * 3 : 0) + Min_zgmZ3g(x) + Max_uzcZ3A(x) + (x.Length > 0 ? x[^1] : 0) + Array.Find(x, v => v > 2);"),
		Create("return 17;", new[] { 1, 2, 3, 4, 5 }), // 1 + 5 + 5 + 3 + 2 + 1 = 17
		Create("return 0;", new int[] { }),
	];
}
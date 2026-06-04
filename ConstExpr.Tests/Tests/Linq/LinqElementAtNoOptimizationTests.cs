using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests that operations which affect element positions are NOT optimized
/// </summary>
[InheritsTests]
public class LinqElementAtNoOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// OrderBy should  be optimized (changes element positions!)
		var a = x.OrderBy(v => v).ElementAt(0);

		// OrderByDescending should  be optimized
		var b = x.OrderByDescending(v => v).ElementAt(0);

		// Reverse should  be optimized
		var c = x.Reverse().ElementAt(0);

		// Where should  be optimized (changes collection size and indices)
		var d = x.Where(v => v > 2).ElementAt(0);

		// Select should  be optimized (transforms elements)
		var e = x.Select(v => v * 2).ElementAt(0);

		// Distinct should  be optimized (removes duplicates, changes indices)
		var f = x.Distinct().ElementAt(0);

		return a + b + c + d + e + f;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x[0] * 3 + TensorPrimitives.Min(x) + TensorPrimitives.Max(x) + x[^1] + First_O1a9Fw(x);"),
		Create(_ => 17, [ new[] { 1, 2, 3, 4, 5 } ]), // 1 + 5 + 5 + 3 + 2 + 1 + 1 = 18
		Create(_ =>
		{
			throw new ArgumentOutOfRangeException("Index was out of range. Must be non-negative and less than the size of the collection. (Parameter 'index')");
		}, [ System.Array.Empty<int>() ]),
	];
}
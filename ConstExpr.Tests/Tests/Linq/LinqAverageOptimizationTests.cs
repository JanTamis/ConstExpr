namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Average() optimization - verify that AsEnumerable, ToList, ToArray are skipped
/// </summary>
[InheritsTests]
public class LinqAverageOptimizationTests : BaseTest<Func<int[], double>>
{
	public override string TestMethod => GetString(x =>
	{
		// AsEnumerable().Average() => collection.Average() (skip AsEnumerable)
		var a = x.AsEnumerable().Average();

		// ToList().Average() => collection.Average() (skip ToList)
		var b = x.ToList().Average();

		// ToArray().Average() => collection.Average() (skip ToArray)
		var c = x.ToArray().Average();

		// Multiple skip operations
		var d = x.AsEnumerable().ToList().Average();

		// Regular Average (should not be optimized)
		var e = x.Average();

		// Average with selector - AsEnumerable
		var f = x.AsEnumerable().Average(v => v * 2);

		// Average with selector - ToList
		var g = x.ToList().Average(v => v * 3);

		return a + b + c + d + e + f + g;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Average();
			var b = x.Average();
			var c = x.Average();
			var d = x.Average();
			var e = x.Average();
			var f = x.Average(v => v << 1);
			var g = x.Average(v => v * 3);
			
			return a + b + c + d + e + f + g;
			""", Unknown),
		Create("return 20.0;", new[] { 1, 2, 3 }), 
		Create("throw new InvalidOperationException(\"Sequence contains no elements\");", new int[] { }), 
		Create("return 100.0;", new[] { 10 }), 
	];
}

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
		var e = x.Average(v => v);

		// Average with selector - AsEnumerable
		var f = x.AsEnumerable().Average(v => v * 2);

		// Average with selector - ToList
		var g = x.ToList().Average(v => v * 3);

		var h = x.Select(s => s * 2).Average();

		return a + b + c + d + e + f + g + h;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = Average_rZxIuA(x);
			var b = Average_rZxIuA(x);
			var c = Average_rZxIuA(x);
			var d = Average_rZxIuA(x);
			var e = Average_rZxIuA(x);
			var f = Average_XH2brA(x);
			var g = Average_8Pzflw(x);
			var h = Average_XH2brA(x);
			
			return a + b + c + d + e + f + g + h;
			"""),
		Create("return 24D;", new[] { 1, 2, 3 }), 
		Create("throw new InvalidOperationException(\"Sequence contains no elements\");", new int[] { }), 
		Create("return 120D;", new[] { 10 }), 
	];
}

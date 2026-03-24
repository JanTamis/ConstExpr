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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = Average_cnpjgw(x);
			var b = Average_cnpjgw(x);
			var c = Average_cnpjgw(x);
			var d = Average_cnpjgw(x);
			var e = Average_cnpjgw(x);
			var f = Average_Ra0CPg(x);
			var g = Average_Jo5e5A(x);
			var h = Average_Ra0CPg(x);
			
			return a + b + c + d + e + f + g + h;
			""", Unknown),
		Create("return 20.0;", new[] { 1, 2, 3 }), 
		Create("throw new InvalidOperationException(\"Sequence contains no elements\");", new int[] { }), 
		Create("return 100.0;", new[] { 10 }), 
	];
}

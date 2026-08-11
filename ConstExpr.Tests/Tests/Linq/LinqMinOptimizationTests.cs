namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Min() optimization - verify identity lambda removal, Select fusion, and chain optimization
/// </summary>
[InheritsTests]
public class LinqMinOptimizationTests : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Min(v => v) => Min()
		var a = x.Min(v => v);

		// Select(selector).Min() => Min(selector)
		var b = x.Select(v => v * 2).Concat(x).Min();

		// OrderBy().Min() => Min() (ordering doesn't affect min)
		var c = x.OrderBy(v => v).Min();

		// AsEnumerable().ToList().Min() => Min()
		var d = x.AsEnumerable().ToList().Min();

		// Reverse().Min() => Min()
		var e = x.Reverse().Min();

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var tensorPrimitivesMin = TensorPrimitives.Min(x);

			return (tensorPrimitivesMin << 2) + Int32.Min(Min_lB3pdg(x), tensorPrimitivesMin);
			"""),
	];
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for Max() optimization - verify identity lambda removal, Select fusion, and chain optimization
/// </summary>
[InheritsTests]
public class LinqMaxOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Max(v => v) => Max()
		var a = x.Max(v => v);

		// Select(selector).Max() => Max(selector)
		var b = x.Select(v => v * 2).Max();

		// OrderBy().Max() => Max() (ordering doesn't affect max)
		var c = x.OrderBy(v => v).Max();

		// AsEnumerable().ToList().Max() => Max()
		var d = x.AsEnumerable().ToList().Max();

		// Reverse().Max() => Max()
		var e = x.Reverse().Concat(x).Max();

		return a + b + c + d + e;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return TensorPrimitives.Max(x) * 4 + Max_JcFfKg(x);"),
		Create(_ => 18, [ new[] { 1, 2, 3 } ]),
		Create(_ => 30, [ new[] { 5 } ])
	];
}
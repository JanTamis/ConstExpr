using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
/// Tests for FirstOrDefault() optimization - verify that unnecessary operations before FirstOrDefault() are removed
/// </summary>
[InheritsTests]
public class LinqFirstOrDefaultOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Where(...).FirstOrDefault() => FirstOrDefault(predicate)
		var a = x.Where(v => v > 3).FirstOrDefault();

		// AsEnumerable().FirstOrDefault() => FirstOrDefault()
		var b = x.AsEnumerable().FirstOrDefault();

		// ToList().FirstOrDefault() => FirstOrDefault()
		var c = x.ToList().FirstOrDefault();

		// ToArray().FirstOrDefault() => FirstOrDefault()
		var d = x.ToArray().FirstOrDefault();

		// AsEnumerable().Where().FirstOrDefault() => FirstOrDefault(predicate)
		var e = x.AsEnumerable().Where(v => v > 2).FirstOrDefault();

		// ToList().Where().FirstOrDefault() => FirstOrDefault(predicate)
		var f = x.ToList().Where(v => v < 5).FirstOrDefault();

		// Complex: AsEnumerable().ToList().Where().FirstOrDefault() => FirstOrDefault(predicate)
		var g = x.AsEnumerable().ToList().Where(v => v == 3).FirstOrDefault();

		// OrderBy should NOT be optimized (changes which element is first!)
		var h = x.OrderBy(v => v).FirstOrDefault();

		// Reverse should NOT be optimized (changes which element is first!)
		var i = x.Reverse().FirstOrDefault();

		// Distinct should NOT be optimized (first element might be duplicate!)
		var j = x.Distinct().FirstOrDefault();

		// Array conditional: x.FirstOrDefault() => x.Length > 0 ? x[0] : default
		var k = x.FirstOrDefault();

		var l = x.Where(v => v > 0).Select(s => s * 2).FirstOrDefault();

		return a + b + c + d + e + f + g + h + i + j + k + l;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Array.Find(x, v => v > 0) * 2 + (x.Length > 0 ? x[0] * 5 : 0) + Array.Find(x, v => v > 3) + Array.Find(x, v => v > 2) + Array.Find(x, v => v < 5) + Array.Find(x, v => v == 3) + TensorPrimitives.Min(x) + (x.Length > 0 ? x[^1] : 0);"),
		Create(_ => 24, [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => 0, [ new int[] { } ]),
	];
}
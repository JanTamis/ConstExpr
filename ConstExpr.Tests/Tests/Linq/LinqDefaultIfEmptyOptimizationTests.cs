using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for DefaultIfEmpty() optimization - verify that unnecessary operations before DefaultIfEmpty() are removed
/// </summary>
[InheritsTests]
public class LinqDefaultIfEmptyOptimizationTests() : BaseTest<Func<int[], int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x =>
	{
		// Simple DefaultIfEmpty
		var a = x.DefaultIfEmpty().Count();

		// Distinct().DefaultIfEmpty() => DefaultIfEmpty()
		var b = x.Distinct().DefaultIfEmpty().Count();

		// OrderBy(...).DefaultIfEmpty() => DefaultIfEmpty()
		var c = x.OrderBy(v => v).DefaultIfEmpty().Count();

		// OrderByDescending(...).DefaultIfEmpty() => DefaultIfEmpty()
		var d = x.OrderByDescending(v => v).DefaultIfEmpty().Count();

		// Reverse().DefaultIfEmpty() => DefaultIfEmpty()
		var e = x.Reverse().DefaultIfEmpty().Count();

		// AsEnumerable().DefaultIfEmpty() => DefaultIfEmpty()
		var f = x.AsEnumerable().DefaultIfEmpty().Count();

		// ToList().DefaultIfEmpty() => DefaultIfEmpty()
		var g = x.ToList().DefaultIfEmpty().Count();

		// ToArray().DefaultIfEmpty() => DefaultIfEmpty()
		var h = x.ToArray().DefaultIfEmpty().Count();

		// Chained operations: Distinct().OrderBy().Reverse().DefaultIfEmpty() => DefaultIfEmpty()
		var i = x.Distinct().OrderBy(v => v).Reverse().DefaultIfEmpty().Count();

		// DefaultIfEmpty().DefaultIfEmpty() => DefaultIfEmpty() (idempotent)
		var j = x.DefaultIfEmpty().DefaultIfEmpty().Count();

		return a + b + c + d + e + f + g + h + i + j;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Int32.Max(x.Length, 1) * 8 + Int32.Max(Count_w6J_9Q(x), 1) * 2;"),
		Create(_ => 50, [ new[] { 1, 2, 3, 4, 5 } ]), // Non-empty: each DefaultIfEmpty returns 5 elements, so 5*10 = 50
		Create(_ => 10, [ System.Array.Empty<int>() ]) // Empty: each DefaultIfEmpty returns 1 element (default), so 1*10 = 10
	];
}
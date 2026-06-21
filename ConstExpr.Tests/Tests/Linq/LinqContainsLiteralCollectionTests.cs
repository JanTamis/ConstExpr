namespace ConstExpr.Tests.Linq;

/// <summary>
///   Tests for [1, 2, 3].Contains(x) => x is 1 or 2 or 3
///   When the collection is a small literal set and the search value is unknown, generate an is-pattern
///   instead of Array.IndexOf for better JIT optimization opportunities.
/// </summary>
[InheritsTests]
public class LinqContainsLiteralCollectionTests : BaseTest<Func<int[], int, bool>>
{
	public override string TestMethod => GetString((values, x) =>
	{
		return values.Contains(x);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// The is-pattern x is 1 or 2 or 3 gets further optimized by the is-pattern optimizer
		// to a range check since 1, 2, 3 are consecutive integers: (uint)(x - 1) <= 2U
		Create("return (uint)(x - 1) <= 2U;", new[] { 1, 2, 3 }, Unknown),
		Create("return (uint)(x - 1) <= 7U && (0x91u >> x - 1 & 1) != 0;", new[] { 1, 5, 8 }, Unknown),
		Create("return (uint)(x - 1) <= 4U && (0x15u >> x - 1 & 1) != 0;", new[] { 1, 3, 5 }, Unknown)
	];
}
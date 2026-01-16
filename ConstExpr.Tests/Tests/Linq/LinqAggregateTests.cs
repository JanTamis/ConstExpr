namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ aggregate operations - verify constant folding for aggregate functions
/// </summary>
[InheritsTests]
public class LinqAggregateTests : BaseTest<Func<int, long>>
{
	public override string TestMethod => GetString((x) =>
	{
		// Sum of constant array
		var a = new[] { 1, 2, 3, 4, 5 }.Sum();

		// Average of constant array
		var b = (int)new[] { 2, 4, 6 }.Average();

		// Count should be optimized
		var c = new[] { 1, 2, 3 }.Count();

		// Min/Max of constant values
		var d = new[] { 5, 2, 8, 1 }.Min();
		var e = new[] { 5, 2, 8, 1 }.Max();

		// Any/All with constant predicates
		var f = new[] { 1, 2, 3 }.Any(v => v > 0) ? 1 : 0;
		var g = new[] { 1, 2, 3 }.All(v => v > 0) ? 1 : 0;

		return a + b + c + d + e + f + g;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = 15;
			var b = 4;
			var c = 3;
			var d = 1;
			var e = 8;
			var f = 1;
			var g = 1;

			return 33L;
			""", 0),
		Create("return 33L;", 10),
		Create("return 33L;", -5),
	];
}


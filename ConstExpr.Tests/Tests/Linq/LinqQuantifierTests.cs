namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ quantifier operations - verify constant folding for Any, All, Contains
/// </summary>
[InheritsTests]
public class LinqQuantifierTests : BaseTest<Func<IEnumerable<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Any with predicate
		var a = x.Any(v => v > 3) ? 1 : 0;

		// Any without predicate
		var b = x.Any() ? 1 : 0;

		// All with predicate that's true for all
		var c = x.All(v => v > 0) ? 1 : 0;

		// All with predicate that's false for some
		var d = x.All(v => v > 2) ? 1 : 0;

		// Contains with element in sequence
		var e = x.Contains(3) ? 1 : 0;

		// Contains with element not in sequence
		var f = x.Contains(10) ? 1 : 0;

		// SequenceEqual with same sequences
		var g = x.SequenceEqual(new[] { 1, 2, 3, 4, 5 }) ? 1 : 0;

		// SequenceEqual with different sequences
		var h = x.SequenceEqual(new[] { 1, 2, 4 }) ? 1 : 0;

		// Complex predicate with Any
		var i = x.Any(v => v > 2 && v < 5) ? 1 : 0;

		// Complex predicate with All
		var j = x.All(v => v > 0 && v <= 5) ? 1 : 0;

		// Any on empty after filter
		var k = x.Where(v => v > 10).Any() ? 1 : 0;

		return a + b + c + d + e + f + g + h + i + j + k;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Any(v => v > 3) ? 1 : 0;
			var b = x.Any() ? 1 : 0;
			var c = x.All(v => v > 0) ? 1 : 0;
			var d = x.All(v => v > 2) ? 1 : 0;
			var e = x.Contains(3) ? 1 : 0;
			var f = x.Contains(10) ? 1 : 0;
			var g = x.SequenceEqual(new[]
			{
				1,
				2,
				3,
				4,
				5
			}) ? 1 : 0;
			var h = x.SequenceEqual(new[]
			{
				1,
				2,
				4
			}) ? 1 : 0;
			var i = x.Any(v => v > 2 && v < 5) ? 1 : 0;
			var j = x.All(v => v > 0 && v <= 5) ? 1 : 0;
			var k = x.Where(v => v > 10).Any() ? 1 : 0;

			return a + b + c + d + e + f + g + h + i + j + k;
			""", Unknown),
		Create("return 7;", new[] { 1, 2, 3, 4, 5 }),
	];
}

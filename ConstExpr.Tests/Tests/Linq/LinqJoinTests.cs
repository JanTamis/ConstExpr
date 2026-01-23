namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ join operations - verify constant folding for Join, GroupJoin, Zip
/// </summary>
[InheritsTests]
public class LinqJoinTests : BaseTest<Func<IEnumerable<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Simple Join
		var a = x.Join(
				new[] { 2, 3, 4 },
				outer => outer,
				inner => inner,
				(o, i) => o + i)
			.Sum();

		// Zip operation
		var b = x.Zip(new[] { 10, 20, 30, 40, 50 }, (first, second) => first + second).Sum();

		// Zip with tuples (newer overload)
		var c = x.Zip(new[] { 4, 5, 6, 7, 8 })
			.Select(tuple => tuple.First * tuple.Second)
			.Sum();

		// GroupJoin count
		var d = x.GroupJoin(
				new[] { 1, 1, 2, 3, 3, 3, 4, 5 },
				outer => outer,
				inner => inner,
				(o, group) => group.Count())
			.Sum();

		// SelectMany (flattening operation)
		var e = x.Select(v => new[] { v, v * 2 })
			.SelectMany(arr => arr)
			.Sum();

		// SelectMany with selector
		var f = x.SelectMany(v => new[] { v, v + 10 }, (original, transformed) => transformed)
			.Count();

		return a + b + c + d + e + f;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Join(new[]
			{
				2,
				3,
				4
			}, outer => outer, inner => inner, (o, i) => o + i).Sum();
			var b = x.Zip(new[]
			{
				10,
				20,
				30,
				40,
				50
			}, (first, second) => first + second).Sum();
			var c = x.Zip(new[]
			{
				4,
				5,
				6,
				7,
				8
			}).Select(tuple => tuple.First * tuple.Second).Sum();
			var d = x.GroupJoin(new[]
			{
				1,
				1,
				2,
				3,
				3,
				3,
				4,
				5
			}, outer => outer, inner => inner, (o, group) => group.Count()).Sum();
			var e = x.Select(v => new[]
			{
				v,
				v * 2
			}).SelectMany(arr => arr).Sum();
			var f = x.SelectMany(v => new[]
			{
				v,
				v + 10
			}, (original, transformed) => transformed).Count();

			return a + b + c + d + e + f;
			""", Unknown),
		Create("return 165;", new[] { 1, 2, 3, 4, 5 }),
	];
}

namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for LINQ set operations - verify constant folding for Distinct, Union, Intersect, Except
/// </summary>
[InheritsTests]
public class LinqSetOperationsTests : BaseTest<Func<IEnumerable<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Distinct on duplicated IEnumerable
		var a = x.Concat(x).Distinct().Count();

		// Union of IEnumerable with another sequence
		var b = x.Union(new[] { 3, 4, 5 }).Count();

		// Intersect with another sequence
		var c = x.Intersect(new[] { 3, 4, 5, 6 }).Count();

		// Except - elements in first but not in second
		var d = x.Except(new[] { 3, 4 }).Count();

		// Concat with another sequence
		var e = x.Concat(new[] { 6, 7 }).Count();

		// DistinctBy
		var f = x.DistinctBy(v => v % 3).Count();

		// UnionBy
		var g = x.UnionBy(new[] { 4, 5, 6 }, v => v % 3).Count();

		// IntersectBy
		var h = x.IntersectBy(new[] { 0, 3, 6 }, v => v % 3).Count();

		// ExceptBy
		var i = x.ExceptBy(new[] { 0, 3 }, v => v % 3).Count();

		return a + b + c + d + e + f + g + h + i;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Concat(x).Distinct().Count();
			var b = x.Union(new[]
			{
				3,
				4,
				5
			}).Count();
			var c = x.Intersect(new[]
			{
				3,
				4,
				5,
				6
			}).Count();
			var d = x.Except(new[]
			{
				3,
				4
			}).Count();
			var e = x.Concat(new[]
			{
				6,
				7
			}).Count();
			var f = x.DistinctBy(v => v % 3).Count();
			var g = x.UnionBy(new[]
			{
				4,
				5,
				6
			}, v => v % 3).Count();
			var h = x.IntersectBy(new[]
			{
				0,
				3,
				6
			}, v => v % 3).Count();
			var i = x.ExceptBy(new[]
			{
				0,
				3
			}, v => v % 3).Count();

			return a + b + c + d + e + f + g + h + i;
			""", Unknown),
		Create("return 30;", new[] { 1, 2, 3, 4, 5 }),
	];
}

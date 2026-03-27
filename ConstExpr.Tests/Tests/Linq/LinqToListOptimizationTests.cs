namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for ToList() optimization - verify redundant materialization removal and chain optimization
/// </summary>
[InheritsTests]
public class LinqToListOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// ToList().ToList() => ToList()
		var a = x.ToList().ToList().Count;

		// ToArray().ToList() => ToList()
		var b = x.ToArray().ToList().Count;

		// AsEnumerable().ToArray().ToList() => ToList()
		var c = x.AsEnumerable().ToArray().ToList().Count;

		return a + b + c;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = x.Length;
			var b = x.Length;
			var c = x.Length;

			return a + b + c;
			""", Unknown),
		Create("return 9;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}

/// <summary>
/// Tests for list.Where(p).ToList() => list.FindAll(p)
/// </summary>
[InheritsTests]
public class LinqWhereToListOptimizationTests : BaseTest<Func<List<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// list.Where(p).ToList() => list.FindAll(p)
		var a = x.Where(v => v > 2).ToList().Count;

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = x.FindAll(v => v > 2).Count;
			
			return a;
			""", Unknown),
		Create("return 3;", new List<int> { 1, 2, 3, 4, 5 }),
		Create("return 0;", new List<int>()),
	];
}

/// <summary>
/// Tests for list.Select(f).Where(p).ToList() => list.FindAll(x => p(f(x)))
/// </summary>
[InheritsTests]
public class LinqSelectWhereToListOptimizationTests : BaseTest<Func<List<int>, int>>
{
	public override string TestMethod => GetString(x =>
	{
		// list.Select(f).Where(p).ToList() => list.FindAll(x => p(f(x)))
		var a = x.Select(v => v * 2).Where(v => v > 4).ToList().Count;

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var a = x.FindAll(v => v << 1 > 4).Count;
			
			return a;
			""", Unknown),
		Create("return 3;", new List<int> { 1, 2, 3, 4, 5 }),
		Create("return 0;", new List<int>()),
	];
}

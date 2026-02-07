namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for ToArray() optimization - verify redundant materialization removal and chain optimization
/// </summary>
[InheritsTests]
public class LinqToArrayOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// ToArray().ToArray() => ToArray()
		var a = x.ToArray().ToArray().Length;

		// ToList().ToArray() => ToArray()
		var b = x.ToList().ToArray().Length;

		// AsEnumerable().ToList().ToArray() => ToArray()
		var c = x.AsEnumerable().ToList().ToArray().Length;

		return a + b + c;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var a = x.Count();
			var b = x.Count();
			var c = x.Count();

			return a + b + c;
			""", Unknown),
		Create("return 9;", new[] { 1, 2, 3 }),
		Create("return 0;", new int[] { }),
	];
}


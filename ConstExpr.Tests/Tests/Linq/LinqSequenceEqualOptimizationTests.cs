namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for SequenceEqual() optimization - verify same sequence returns true
/// </summary>
[InheritsTests]
public class LinqSequenceEqualOptimizationTests : BaseTest<Func<int[], bool>>
{
	public override string TestMethod => GetString(x =>
	{
		// SequenceEqual(same) => true
		var a = x.SequenceEqual(x);

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return true;", Unknown),
		Create("return true;", new[] { 1, 2, 3 }),
		Create("return true;", new int[] { }),
	];
}


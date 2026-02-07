namespace ConstExpr.Tests.Tests.Linq;

/// <summary>
/// Tests for Reverse() optimization - verify double reverse removal
/// </summary>
[InheritsTests]
public class LinqReverseOptimizationTests : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(x =>
	{
		// Reverse().Reverse() => original
		var a = x.Reverse().Reverse().First();

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return a[0];", Unknown),
		Create("return 1;", new[] { 1, 2, 3 }),
		Create("return 5;", new[] { 5 }),
	];
}


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

		var b = Enumerable.Empty<int>().SequenceEqual(x);

		var c = x.SequenceEqual(Enumerable.Empty<int>());

		return a && b && c;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var b = x.Count <= 0;
			var c = x.Count <= 0;

			return b && c;
			""", Unknown),
		Create("return false;", new[] { 1, 2, 3 }),
		Create("return true;", new int[] { }),
	];
}
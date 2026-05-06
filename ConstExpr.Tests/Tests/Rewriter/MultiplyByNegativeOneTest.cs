namespace ConstExpr.Tests.Rewriter;

/// <summary>x * -1 → -x.</summary>
[InheritsTests]
public class MultiplyByNegativeOneTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x * -1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return -x;"),
		Create("return -7;", 7),
		Create("return 3;", -3),
	];
}
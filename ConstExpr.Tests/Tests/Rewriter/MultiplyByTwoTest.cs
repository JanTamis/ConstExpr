namespace ConstExpr.Tests.Rewriter;

/// <summary>x * 2 → x + x (strength reduction) or x &lt;&lt; 1.</summary>
[InheritsTests]
public class MultiplyByTwoTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => x * 2);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x << 1;"),
		Create("return 10;", 5),
		Create("return -6;", -3),
	];
}
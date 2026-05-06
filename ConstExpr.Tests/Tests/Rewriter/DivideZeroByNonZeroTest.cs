namespace ConstExpr.Tests.Rewriter;

/// <summary>0 / x = 0 when x != 0.</summary>
[InheritsTests]
public class DivideZeroByNonZeroTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x => 0 / x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 0;"),
		Create("return 0;", 7),
		Create("return 0;", -2),
	];
}
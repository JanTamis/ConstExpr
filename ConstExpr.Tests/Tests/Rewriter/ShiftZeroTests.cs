namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for left-shift of zero: 0 << n = 0.</summary>
[InheritsTests]
public class ShiftZeroTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		return 0 << n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 0;"),
		Create("return 0;", 5),
		Create("return 0;", 0),
	];
}
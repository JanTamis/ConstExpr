namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for left-shift of zero: 0 << n = 0.</summary>
[InheritsTests]
public class ShiftZeroTests : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		return 0 << n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 0),
	];
}
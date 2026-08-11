namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Test with consecutive values
/// </summary>
[InheritsTests]
public class PatternBitmaskConsecutiveTest : BaseTestWithRandomValues<Func<int, bool>>
{
	public override string TestMethod => GetString(n =>
	{
		return n is 5 or 6 or 7 or 8;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => (uint) (n - 5) <= 3U),
	];
}
namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Test with small set (powers of 2)
/// </summary>
[InheritsTests]
public class PatternBitmaskSmallTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(n =>
	{
		return n is 2 or 4 or 8;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => (uint) (n - 2) <= 6U && (n & n - 1) == 0),
		CreateFolded(2),
		CreateFolded(4),
		CreateFolded(8),
		CreateFolded(1),
		CreateFolded(3),
		CreateFolded(5)
	];
}
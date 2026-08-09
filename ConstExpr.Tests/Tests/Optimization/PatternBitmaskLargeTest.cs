namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Test with larger set of values
/// </summary>
[InheritsTests]
public class PatternBitmaskLargeTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(n =>
	{
		return n is 0 or 10 or 20 or 30 or 40 or 50 or 60 or 70 or 80;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => (uint) n <= 80U && n % 10 == 0),
		CreateFolded(0),
		CreateFolded(10),
		CreateFolded(20),
		CreateFolded(30),
		CreateFolded(40),
		CreateFolded(50),
		CreateFolded(60),
		CreateFolded(5),
		CreateFolded(25)
	];
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
/// Test with consecutive values
/// </summary>
[InheritsTests]
public class PatternBitmaskConsecutiveTest() : BaseTest<Func<int, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		return n is 5 or 6 or 7 or 8;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (uint)(n - 5) <= 3U;"),
		Create("return true;", 5),
		Create("return true;", 6),
		Create("return true;", 7),
		Create("return true;", 8),
		Create("return false;", 4),
		Create("return false;", 9),
	];
}
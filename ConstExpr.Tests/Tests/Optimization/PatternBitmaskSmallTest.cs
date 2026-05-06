using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
/// Test with small set (powers of 2)
/// </summary>
[InheritsTests]
public class PatternBitmaskSmallTest() : BaseTest<Func<int, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		return n is 2 or 4 or 8;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (uint)(n - 2) <= 6U && (n & n - 1) == 0;"),
		Create("return true;", 2),
		Create("return true;", 4),
		Create("return true;", 8),
		Create("return false;", 1),
		Create("return false;", 3),
		Create("return false;", 5),
	];
}
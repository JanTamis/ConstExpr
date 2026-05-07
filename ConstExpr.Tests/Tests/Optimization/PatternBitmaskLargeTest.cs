using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
/// Test with larger set of values
/// </summary>
[InheritsTests]
public class PatternBitmaskLargeTest() : BaseTest<Func<int, bool>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n =>
	{
		return n is 0 or 10 or 20 or 30 or 40 or 50 or 60;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (uint)n <= 60U && (0x1004010040100401UL >> n & 1) != 0;"),
		Create("return true;", 0),
		Create("return true;", 10),
		Create("return true;", 20),
		Create("return true;", 30),
		Create("return true;", 40),
		Create("return true;", 50),
		Create("return true;", 60),
		Create("return false;", 5),
		Create("return false;", 25),
	];
}
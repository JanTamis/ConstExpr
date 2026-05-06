using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
/// Tests for pattern matching bitmask optimization.
/// Verifies that patterns like "x is 1 or 5 or 10 or 15 or 20" are optimized
/// into efficient bitmask checks.
/// </summary>
[InheritsTests]
public class PatternBitmaskTest() : BaseTest<Func<int, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		return n is 1 or 5 or 10 or 15 or 20;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (uint)(n - 1) <= 19U && (0x84211u >> n - 1 & 1) != 0;"), // Unknown value
		Create("return true;", 1),   // Match
		Create("return true;", 5),   // Match
		Create("return true;", 10),  // Match
		Create("return true;", 15),  // Match
		Create("return true;", 20),  // Match
		Create("return false;", 0),  // No match
		Create("return false;", 3),  // No match
		Create("return false;", 7),  // No match
		Create("return false;", 21), // No match
	];
}
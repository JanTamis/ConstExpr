namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Tests for pattern matching bitmask optimization.
///   Verifies that patterns like "x is 1 or 5 or 10 or 15 or 20" are optimized
///   into efficient bitmask checks.
/// </summary>
[InheritsTests]
public class PatternBitmaskTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(n =>
	{
		return n is 1 or 5 or 10 or 15 or 20;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			var diff = n - 1;

			return (uint) diff <= 19U && (0x84211u >> diff & 1) != 0;
		}), // Unknown value
		CreateFolded(1), // Match
		CreateFolded(5), // Match
		CreateFolded(10), // Match
		CreateFolded(15), // Match
		CreateFolded(20), // Match
		CreateFolded(0), // No match
		CreateFolded(3), // No match
		CreateFolded(7), // No match
		CreateFolded(21) // No match
	];
}
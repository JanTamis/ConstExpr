namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Tests for pattern matching bitmask optimization.
///   Verifies that patterns like "x is 1 or 5 or 10 or 15 or 20" are optimized
///   into efficient bitmask checks.
/// </summary>
[InheritsTests]
public class PatternBitmaskTest : BaseTestWithRandomValues<Func<int, bool>>
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
		}),
	];
}
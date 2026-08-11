namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Test with byte values
/// </summary>
[InheritsTests]
public class PatternBitmaskByteTest : BaseTestWithRandomValues<Func<byte, bool>>
{
	public override string TestMethod => GetString(n =>
	{
		return n is 1 or 3 or 7;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			var diff = n - 1;

			return diff <= 6 && (0x45u >> diff & 1) != 0;
		}),
	];
}
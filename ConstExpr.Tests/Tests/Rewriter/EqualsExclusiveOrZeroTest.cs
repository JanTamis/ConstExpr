namespace ConstExpr.Tests.Rewriter;

/// <summary>Tests for XOR-zero equality: (a ^ b) == 0 => a == b.</summary>
[InheritsTests]
public class EqualsExclusiveOrZeroTest : BaseTestWithRandomValues<Func<int, int, bool>>
{
	public override string TestMethod => GetString((a, b) => (a ^ b) == 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) => a == b)
	];
}
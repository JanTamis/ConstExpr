namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Covers the reversed-operand branch (null == s) — a distinct check from s == null in the fold.
/// </summary>
[InheritsTests]
public class NullEqualityReversedOperandTest : BaseTestWithRandomValues<Func<string, bool>>
{
	public override string TestMethod => GetString(s => null == s);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => false),
	];
}
namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Verifies that an unsigned "greater than zero" comparison collapses to a plain
///   inequality: (uint)x &gt; 0 =&gt; x != 0. Unsigned integers can never be negative, so the
///   two are equivalent, but the rewritten form needs no range check.
/// </summary>
[InheritsTests]
public class GreaterThanUnsignedZeroTest : BaseTestWithRandomValues<Func<uint, bool>>
{
	public override string TestMethod => GetString(x => x > 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x != 0u),
	];
}

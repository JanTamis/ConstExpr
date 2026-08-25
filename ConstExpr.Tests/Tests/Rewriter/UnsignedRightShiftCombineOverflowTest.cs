namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Verifies that combining two &gt;&gt;&gt; shifts whose counts sum to at least the operand's
///   bit width folds to the literal 0 - &gt;&gt;&gt; is always logical (zero-fill), so every bit is
///   shifted out past the boundary regardless of x's value.
/// </summary>
[InheritsTests]
public class UnsignedRightShiftCombineOverflowTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x >>> 20 >>> 20);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 0),
	];
}

namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Verifies that combining two left shifts whose counts sum to at least the operand's bit
///   width folds to the literal 0, not a naive x &lt;&lt; (a + b) (which would mask the summed
///   count back down to a nonzero shift and silently miscompile).
/// </summary>
[InheritsTests]
public class LeftShiftCombineOverflowTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x << 20 << 20);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 0),
	];
}

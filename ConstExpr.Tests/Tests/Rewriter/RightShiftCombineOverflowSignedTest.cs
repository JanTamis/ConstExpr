namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Verifies that combining two arithmetic (signed) right shifts whose counts sum to at
///   least the operand's bit width folds to x &gt;&gt; (bitWidth - 1) - a single shift that
///   saturates to the sign bit at runtime - rather than a naive x &gt;&gt; (a + b), which would
///   mask the summed count back down to a smaller, wrong shift.
/// </summary>
[InheritsTests]
public class RightShiftCombineOverflowSignedTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x => x >> 20 >> 20);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x >> 31),
	];
}

using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class UnsignedRightShiftZeroTest() : BaseTest<Func<int, int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x => 0 >>> x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 0),
		CreateFolded(3),
		CreateFolded(31)
	];
}
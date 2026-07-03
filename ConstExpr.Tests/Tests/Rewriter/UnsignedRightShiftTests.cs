using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class UnsignedRightShiftByZeroTest() : BaseTest<Func<int, int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x => x >>> 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x),
		Create(_ => 5, [ 5 ]),
		Create(_ => -1, [ -1 ])
	];
}
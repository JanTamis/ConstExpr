using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class UnsignedRightShiftCombineTest() : BaseTest<Func<int, int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x => x >>> 2 >>> 3);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x >>> 5),
		Create(_ => 1, [ 32 ]),
		Create(_ => 0, [ 0 ])
	];
}
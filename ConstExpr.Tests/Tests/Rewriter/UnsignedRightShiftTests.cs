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

[InheritsTests]
public class UnsignedRightShiftZeroTest() : BaseTest<Func<int, int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x => 0 >>> x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(_ => 0),
		Create(_ => 0, [ 3 ]),
		Create(_ => 0, [ 31 ])
	];
}

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
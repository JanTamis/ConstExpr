using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class UnsignedRightShiftByZeroTest() : BaseTest<Func<int, int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x => x >>> 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x;"),
		Create("return 5;", 5),
		Create("return -1;", -1)
	];
}

[InheritsTests]
public class UnsignedRightShiftZeroTest() : BaseTest<Func<int, int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x => 0 >>> x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 0;"),
		Create("return 0;", 3),
		Create("return 0;", 31)
	];
}

[InheritsTests]
public class UnsignedRightShiftCombineTest() : BaseTest<Func<int, int>>(FastMathFlags.AssociativeMath)
{
	public override string TestMethod => GetString(x => x >>> 2 >>> 3);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x >>> 5;"),
		Create("return 1;", 32),
		Create("return 0;", 0)
	];
}
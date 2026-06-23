namespace ConstExpr.Tests.Rewriter;

// Family 1: -(~x) => x + 1   (two's complement: ~x == -x - 1)
[InheritsTests]
public class TwosComplementNegateOfComplementTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n => -~n);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return n + 1;", Unknown),
		Create("return 6;", 5), // ~5 = -6, -(-6) = 6
		Create("return 1;", 0) // ~0 = -1, -(-1) = 1
	];
}

// Family 1: ~(-x) => x - 1
[InheritsTests]
public class TwosComplementComplementOfNegateTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n => ~-n);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return n - 1;", Unknown),
		Create("return 4;", 5), // -5, ~(-5) = 4
		Create("return -1;", 0) // ~0 = -1
	];
}

// Family 2: ~(x - 1) => -x
[InheritsTests]
public class ComplementOfPlusMinusOneMinusTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n => ~(n - 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return -n;", Unknown),
		Create("return -5;", 5), // ~(5 - 1) = ~4 = -5
		Create("return 0;", 0) // ~(0 - 1) = ~(-1) = 0
	];
}

// Family 2 with a 64-bit operand: exercises the long/nint fire path of the guard.
[InheritsTests]
public class ComplementOfMinusOneLongTest : BaseTest<Func<long, long>>
{
	public override string TestMethod => GetString(n => ~(n - 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return -n;", Unknown),
		Create("return -5L;", 5L),
		Create("return 0L;", 0L)
	];
}

// Family 2: ~(x + 1) => -x - 2
[InheritsTests]
public class ComplementOfPlusMinusOnePlusTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n => ~(n + 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return -n - 2;", Unknown),
		Create("return -7;", 5), // ~(5 + 1) = ~6 = -7
		Create("return -2;", 0) // ~(0 + 1) = ~1 = -2
	];
}
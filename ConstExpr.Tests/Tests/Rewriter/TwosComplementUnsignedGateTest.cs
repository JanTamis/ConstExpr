using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

// Gate: ~(x - 1) must NOT become -x for ulong (-ulong is a compile error).
[InheritsTests]
public class TwosComplementUlongGateTest : BaseTest<Func<ulong, ulong>>
{
	public override string TestMethod => GetString(n => ~(n - 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Rewrite must NOT fire: body stays ~(n - 1) (literal normalised to 1UL), never -n.
		Create("return ~(n - 1UL);", Unknown)
	];
}

// Gate: ~(x - 1) must NOT become -x for uint (would wrap in 32-bit unsigned arithmetic).
[InheritsTests]
public class TwosComplementUintGateTest : BaseTest<Func<uint, uint>>
{
	public override string TestMethod => GetString(n => ~(n - 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return ~(n - 1U);", Unknown)
	];
}

// Gate: -(C + b) must NOT fire for unsigned b (would diverge when the addition wraps).
[InheritsTests]
public class NegateAdditionUintGateTest() : BaseTest<Func<uint, long>>(FastMathFlags.NoSignedZero)
{
	public override string TestMethod => GetString(n => -(5 + n));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Rewrite must NOT fire for unsigned: only the commutative reorder remains, never -5 - n.
		Create("return -(n + 5U);", Unknown)
	];
}
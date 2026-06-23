using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

// Family 3 (integer): -(C + b) => (-C) - b  — always safe, fires under Strict
[InheritsTests]
public class NegateAdditionIntTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n => -(5 + n));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return -5 - n;", Unknown),
		Create("return -15;", 10), // -(5 + 10) = -15
		Create("return -5;", 0) // -(5 + 0) = -5
	];
}

// Family 3 (floating-point): requires NoSignedZero
[InheritsTests]
public class NegateAdditionDoubleTest() : BaseTest<Func<double, double>>(FastMathFlags.NoSignedZero)
{
	public override string TestMethod => GetString(f => -(5D + f));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return -5D - f;", Unknown),
		Create("return -15D;", 10D),
		Create("return -5D;", 0D)
	];
}

// Family 3 (floating-point) under Strict: must NOT fire (signed-zero divergence)
[InheritsTests]
public class NegateAdditionStrictTest : BaseTest<Func<double, double>>
{
	public override string TestMethod => GetString(f => -(5D + f));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Family 3 must NOT fire under Strict for floats; only the flag-independent
		// commutative canonicalisation (5D + f => f + 5D) remains.
		Create("return -(f + 5D);", Unknown),
		Create("return -15D;", 10D),
		Create("return -5D;", 0D)
	];
}
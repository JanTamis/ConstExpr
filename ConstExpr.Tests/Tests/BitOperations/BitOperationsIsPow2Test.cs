using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.BitOperations;

[InheritsTests]
public class BitOperationsIsPow2UintTest() : BaseTest<Func<uint, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Numerics.BitOperations.IsPow2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x != 0U && (x & x - 1U) == 0U;"),
		Create("return true;", 8u),
		Create("return true;", 1024u),
		Create("return false;", 0u),
		Create("return false;", 7u),
		Create("return false;", 6u)
	];
}

[InheritsTests]
public class BitOperationsIsPow2IntTest() : BaseTest<Func<int, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Numerics.BitOperations.IsPow2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return x > 0 && (x & x - 1) == 0;"),
		Create("return true;", 16),
		Create("return false;", 0),
		Create("return false;", -4),
		Create("return false;", 6)
	];
}
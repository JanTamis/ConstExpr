using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.BitOperations;

[InheritsTests]
public class BitOperationsRotateLeftUintTest() : BaseTest<Func<uint, int, uint>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((value, offset) => System.Numerics.BitOperations.RotateLeft(value, offset));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return value << offset | value >> 32 - offset;"),
		Create("return 8U;", 1u, 3),
		Create("return 2147483648U;", 1u, 31),
		Create("return 1U;", 1u, 0)
	];
}

[InheritsTests]
public class BitOperationsRotateRightUintTest() : BaseTest<Func<uint, int, uint>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((value, offset) => System.Numerics.BitOperations.RotateRight(value, offset));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return value >> offset | value << 32 - offset;"),
		Create("return 1U;", 8u, 3),
		Create("return 8U;", 1u, 29)
	];
}
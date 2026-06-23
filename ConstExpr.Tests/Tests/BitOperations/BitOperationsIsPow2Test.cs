using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.BitOperations;

[InheritsTests]
public class BitOperationsIsPow2UintTest() : BaseTest<Func<uint, bool>>(FastMathFlags.All)
{
	public override string TestMethod => GetString(x => System.Numerics.BitOperations.IsPow2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => true, [ 8u ]),
		Create(_ => true, [ 1024u ]),
		Create(_ => false, [ 0u ]),
		Create(_ => false, [ 7u ]),
		Create(_ => false, [ 6u ])
	];
}

[InheritsTests]
public class BitOperationsIsPow2IntTest() : BaseTest<Func<int, bool>>(FastMathFlags.All)
{
	public override string TestMethod => GetString(x => System.Numerics.BitOperations.IsPow2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => true, [ 16 ]),
		Create(_ => false, [ 0 ]),
		Create(_ => false, [ -4 ]),
		Create(_ => false, [ 6 ])
	];
}
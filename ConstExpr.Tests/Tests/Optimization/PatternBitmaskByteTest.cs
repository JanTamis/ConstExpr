using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
/// Test with byte values
/// </summary>
[InheritsTests]
public class PatternBitmaskByteTest() : BaseTest<Func<byte, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n =>
	{
		return n is 1 or 3 or 7;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			var diff = n - 1;

			return diff <= 6 && (0x45u >> diff & 1) != 0;
		}),
		Create(_ => true, [ (byte) 1 ]),
		Create(_ => true, [ (byte) 3 ]),
		Create(_ => true, [ (byte) 7 ]),
		Create(_ => false, [ (byte) 0 ]),
		Create(_ => false, [ (byte) 4 ])
	];
}
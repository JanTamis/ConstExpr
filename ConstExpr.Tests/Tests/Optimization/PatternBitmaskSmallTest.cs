using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
/// Test with small set (powers of 2)
/// </summary>
[InheritsTests]
public class PatternBitmaskSmallTest() : BaseTest<Func<int, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n =>
	{
		return n is 2 or 4 or 8;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return (uint)(n - 2) <= 6U && (n & n - 1) == 0;"),
		Create(_ => true, [ 2 ]),
		Create(_ => true, [ 4 ]),
		Create(_ => true, [ 8 ]),
		Create(_ => false, [ 1 ]),
		Create(_ => false, [ 3 ]),
		Create(_ => false, [ 5 ]),
	];
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Test with consecutive values
/// </summary>
[InheritsTests]
public class PatternBitmaskConsecutiveTest() : BaseTest<Func<int, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(n =>
	{
		return n is 5 or 6 or 7 or 8;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => (uint) (n - 5) <= 3U),
		Create(_ => true, [ 5 ]),
		Create(_ => true, [ 6 ]),
		Create(_ => true, [ 7 ]),
		Create(_ => true, [ 8 ]),
		Create(_ => false, [ 4 ]),
		Create(_ => false, [ 9 ])
	];
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Tests for pattern matching bitmask optimization.
///   Verifies that patterns like "x is 1 or 5 or 10 or 15 or 20" are optimized
///   into efficient bitmask checks.
/// </summary>
[InheritsTests]
public class PatternBitmaskTest() : BaseTest<Func<int, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n =>
	{
		return n is 1 or 5 or 10 or 15 or 20;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			var diff = n - 1;

			return (uint)diff <= 19U && (0x84211u >> diff & 1) != 0;
		}), // Unknown value
		Create(_ => true, [ 1 ]), // Match
		Create(_ => true, [ 5 ]), // Match
		Create(_ => true, [ 10 ]), // Match
		Create(_ => true, [ 15 ]), // Match
		Create(_ => true, [ 20 ]), // Match
		Create(_ => false, [ 0 ]), // No match
		Create(_ => false, [ 3 ]), // No match
		Create(_ => false, [ 7 ]), // No match
		Create(_ => false, [ 21 ]) // No match
	];
}
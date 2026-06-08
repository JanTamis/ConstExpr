using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class SquareTest() : BaseTest<Func<int, int>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n => n * n);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create(_ => 25, [ 5 ]),
		Create(_ => 0, [ 0 ]),
		Create(_ => 100, [ -10 ])
	];
}
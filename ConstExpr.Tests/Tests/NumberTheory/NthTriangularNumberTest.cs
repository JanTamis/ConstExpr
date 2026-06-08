using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class NthTriangularNumberTest() : BaseTest<Func<int, int>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n => n * (n + 1) / 2);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create(_ => 15, [ 5 ]),
		Create(_ => 1, [ 1 ]),
		Create(_ => 55, [ 10 ])
	];
}
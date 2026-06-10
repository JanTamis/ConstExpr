using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumOfFirstNTest() : BaseTest<Func<int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n => n * (n + 1) / 2);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => 55, [ 10 ]),
		Create(_ => 0, [ 0 ]),
		Create(_ => 5050, [ 100 ])
	];
}
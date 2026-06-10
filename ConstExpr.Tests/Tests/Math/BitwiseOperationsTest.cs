using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class BitwiseOperationsTest() : BaseTest<Func<int, int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => a & b | a ^ b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => 14, [ 12, 10 ]),
		Create((_, _) => 8, [ 8, 8 ]),
		Create((_, _) => 5, [ 5, 0 ])
	];
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class ModuloTest() : BaseTest<Func<int, int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((dividend, divisor) =>
	{
		if (divisor == 0)
		{
			return 0;
		}

		var result = dividend % divisor;

		if (result < 0)
		{
			result += divisor;
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => 3, [ 13, 10 ]),
		Create((_, _) => 2, [ -8, 5 ]),
		Create((_, _) => 0, [ 10, 0 ])
	];
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class IntClampConstantBoundsTest() : BaseTest<Func<int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(value =>
	{
		if (value < 0)
		{
			return 0;
		}

		if (value > 10)
		{
			return 10;
		}

		return value;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(value => Int32.Clamp(value, 0, 10)),
		Create(_ => 5, [ 5 ]),
		Create(_ => 0, [ -5 ]),
		Create(_ => 10, [ 15 ])
	];
}
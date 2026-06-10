using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class DigitSumTest() : BaseTest<Func<int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n =>
	{
		if (n < 0)
		{
			n = -n;
		}

		var sum = 0;

		while (n > 0)
		{
			sum += n % 10;
			n /= 10;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => 6, [ 123 ]),
		Create(_ => 10, [ 1234 ]),
		Create(_ => 0, [ 0 ])
	];
}
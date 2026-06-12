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
		Create("""
			if (n < 0)
				n = -n;

			var sum = 0;

			while (n > 0)
			{
				sum += n - (((int)((long)n * 1717986919 >> 32) >> 2) + ((int)((long)n * 1717986919 >> 32) >> 2 >>> 31)) * 10;
				n = ((int)((long)n * 1717986919 >> 32) >> 2) + ((int)((long)n * 1717986919 >> 32) >> 2 >>> 31);
			}

			return sum;
			"""),
		Create(_ => 6, [ 123 ]),
		Create(_ => 10, [ 1234 ]),
		Create(_ => 0, [ 0 ])
	];
}
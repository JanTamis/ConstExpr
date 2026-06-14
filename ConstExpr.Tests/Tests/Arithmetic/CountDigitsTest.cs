using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class CountDigitsTest() : BaseTest<Func<int, int>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(n =>
	{
		if (n == 0)
		{
			return 1;
		}

		if (n < 0)
		{
			n = -n;
		}

		var count = 0;

		while (n > 0)
		{
			count++;
			n /= 10;
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			switch (n)
			{
				case 0:
					return 1;
				case < 0:
					n = -n;
					break;
			}

			var count = 0;

			while (n > 0)
			{
				count++;
				n = ((int) (n * 1717986919L >> 32) >> 2) - (n >> 31);
			}

			return count;
		}),
		Create(_ => 3, [ 123 ]),
		Create(_ => 1, [ 0 ]),
		Create(_ => 4, [ -4567 ])
	];
}
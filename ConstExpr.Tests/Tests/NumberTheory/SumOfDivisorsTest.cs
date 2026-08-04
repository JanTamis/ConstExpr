namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class SumOfDivisorsTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		if (n <= 0)
		{
			return 0;
		}

		var sum = 0;
		var i = 1;

		while (i <= n)
		{
			if (n % i == 0)
			{
				sum += i;
			}

			i++;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			if (n <= 0)
			{
				return 0;
			}

			var sum = 0;
			var i = 1;

			do
			{
				if (n % i == 0)
				{
					sum += i;
				}

				i++;
			} while (i <= n);

			return sum;
		}),
		Create(_ => 28, [ 12 ]),
		Create(_ => 1, [ 1 ]),
		Create(_ => 0, [ 0 ])
	];
}
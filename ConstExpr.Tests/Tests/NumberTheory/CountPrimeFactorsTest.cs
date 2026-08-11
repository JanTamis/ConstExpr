namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class CountPrimeFactorsTest : BaseTestWithRandomValues<Func<int, int>>
{
	protected override int MaxRandomMagnitudeBits => 4;

	public override string TestMethod => GetString(n =>
	{
		var count = 0;
		var num = System.Math.Abs(n);
		var i = 2;

		while (i * i <= num)
		{
			while (num % i == 0)
			{
				count++;
				num /= i;
			}

			i++;
		}

		if (num > 1)
		{
			count++;
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var count = 0;
			var num = FastAbs(n);
			var i = 2;

			while (i * i <= num)
			{
				while (num % i == 0)
				{
					count++;
					num /= i;
				}

				i++;
			}

			if (num > 1)
				count++;

			return count;
			"""),
	];
}
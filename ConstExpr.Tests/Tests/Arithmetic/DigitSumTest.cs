namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class DigitSumTest : BaseTestWithRandomValues<Func<int, int>>
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
			n = FastAbs(n);

			var sum = 0;

			while (n > 0)
			{
				sum += n % 10;
				n /= 10;
			}

			return sum;
			"""),
	];
}
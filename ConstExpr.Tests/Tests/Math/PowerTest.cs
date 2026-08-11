namespace ConstExpr.Tests.Math;

[InheritsTests]
public class PowerTest : BaseTestWithRandomValues<Func<int, int, long>>
{
	public override string TestMethod => GetString((baseNum, exponent) =>
	{
		if (exponent < 0)
		{
			return 0L;
		}

		if (exponent == 0)
		{
			return 1L;
		}

		var result = 1L;
		var base64 = (long) baseNum;

		while (exponent > 0)
		{
			if (exponent % 2 == 1)
			{
				result *= base64;
			}

			base64 *= base64;
			exponent /= 2;
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((baseNum, exponent) =>
		{
			switch (exponent)
			{
				case < 0:
					return 0L;
				case 0:
					return 1L;
			}

			var result = 1L;
			var base64 = (long) baseNum;

			while (exponent > 0)
			{
				if (Int32.IsOddInteger(exponent))
					result *= base64;

				base64 *= base64;
				exponent >>= 1;
			}

			return result;
		}),
	];
}
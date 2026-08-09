namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class CountDigitsTest : BaseTest<Func<int, int>>
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
		Create("""
			if (n == 0)
				return 1;

			n = FastAbs(n);

			var count = 0;

			do
			{
				count++;
				n /= 10;
			} while (n > 0);

			return count;
			"""),
		CreateFolded(123),
		CreateFolded(0),
		CreateFolded(-4567)
	];
}
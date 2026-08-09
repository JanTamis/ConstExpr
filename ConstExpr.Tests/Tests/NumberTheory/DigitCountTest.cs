namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class DigitCountTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		if (n == 0)
		{
			return 1;
		}

		var count = 0;
		var num = System.Math.Abs(n);

		while (num > 0)
		{
			count++;
			num /= 10;
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			if (n == 0)
				return 1;

			var count = 0;
			var num = FastAbs(n);

			do
			{
				count++;
				num /= 10;
			}
			while (num > 0);

			return count;
			"""),
		CreateFolded(123),
		CreateFolded(0),
		CreateFolded(-1234)
	];
}
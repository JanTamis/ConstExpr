namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class DigitalRootTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		var num = System.Math.Abs(n);

		while (num >= 10)
		{
			var sum = 0;

			while (num > 0)
			{
				sum += num % 10;
				num /= 10;
			}

			num = sum;
		}

		return num;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var num = FastAbs(n);

			while (num >= 10)
			{
				var sum = 0;

				do
				{
					sum += num % 10;
					num /= 10;
				}
				while (num > 0);

				num = sum;
			}

			return num;
			"""),
	];
}
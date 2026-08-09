namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumOfSquaresTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		if (n <= 0)
		{
			return 0;
		}

		var total = 0;

		for (var i = 1; i <= n; i++)
		{
			total += i * i;
		}

		return total;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(5),
		CreateFolded(0),
		CreateFolded(3)
	];
}
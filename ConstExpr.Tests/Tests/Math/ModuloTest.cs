namespace ConstExpr.Tests.Math;

[InheritsTests]
public class ModuloTest : BaseTest<Func<int, int, int>>
{
	public override string TestMethod => GetString((dividend, divisor) =>
	{
		if (divisor == 0)
		{
			return 0;
		}

		var result = dividend % divisor;

		if (result < 0)
		{
			result += divisor;
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(13, 10),
		CreateFolded(-8, 5),
		CreateFolded(10, 0)
	];
}
namespace ConstExpr.Tests.Math;

[InheritsTests]
public class ModuloTest : BaseTestWithRandomValues<Func<int, int, int>>
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
	];
}
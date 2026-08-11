namespace ConstExpr.Tests.Math;

[InheritsTests]
public class IntClampConstantBoundsTest : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(value =>
	{
		if (value < 0)
		{
			return 0;
		}

		if (value > 10)
		{
			return 10;
		}

		return value;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(value => Int32.Clamp(value, 0, 10)),
	];
}
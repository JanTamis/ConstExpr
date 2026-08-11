namespace ConstExpr.Tests.Math;

[InheritsTests]
public class ClampTest : BaseTestWithRandomValues<Func<int, int, int, int>>
{
	public override string TestMethod => GetString((value, min, max) =>
	{
		if (value < min)
		{
			return min;
		}

		if (value > max)
		{
			return max;
		}

		return value;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((value, min, max) => Int32.Clamp(value, min, max)),
	];
}
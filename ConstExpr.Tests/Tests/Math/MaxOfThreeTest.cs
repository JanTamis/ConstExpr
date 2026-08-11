namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MaxOfThreeTest : BaseTestWithRandomValues<Func<int, int, int, int>>
{
	public override string TestMethod => GetString((a, b, c) =>
	{
		var max = a;

		if (b > max)
		{
			max = b;
		}

		if (c > max)
		{
			max = c;
		}

		return max;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b, c) => Int32.Max(c, Int32.Max(b, a))),
	];
}
namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MaxOfThreeTest : BaseTest<Func<int, int, int, int>>
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
		Create((a, b, c) =>
		{
			var max = a;
			max = Int32.Max(b, max);
			max = Int32.Max(c, max);

			return max;
		}),
		Create((_, _, _) => 10, [ 5, 10, 3 ]),
		Create((_, _, _) => 5, [ 5, 5, 5 ])
	];
}
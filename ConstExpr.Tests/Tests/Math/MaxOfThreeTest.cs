using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MaxOfThreeTest() : BaseTest<Func<int, int, int, int>>(FastMathFlags.FastMath)
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
		Create("""
			var max = a;

			if (b > a)
			{
				max = b;
			}

			if (c > max)
			{
				max = c;
			}

			return max;
			"""),
		Create("return 10;", 5, 10, 3),
		Create("return 5;", 5, 5, 5)
	];
}
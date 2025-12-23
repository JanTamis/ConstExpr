using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MaxOfThreeTest() : BaseTest<Func<int, int, int, int>>(FloatingPointEvaluationMode.FastMath)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown, Unknown),
		Create("return 10;", 5, 10, 3),
		Create("return 5;", 5, 5, 5)
	];
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class ClampTest() : BaseTest<Func<int, int, int, int>>(FloatingPointEvaluationMode.FastMath)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown, Unknown),
		Create("return 5;", 5, 0, 10),
		Create("return 0;", -5, 0, 10),
		Create("return 10;", 15, 0, 10)
	];
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class ModuloTest() : BaseTest<Func<int, int, int>>(FloatingPointEvaluationMode.FastMath)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 3;", 13, 10),
		Create("return 2;", -8, 5),
		Create("return 0;", 10, 0)
	];
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class CeilingDivisionTest() : BaseTest<Func<int, int, int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((numerator, divisor) =>
	{
		if (divisor == 0)
		{
			return 0;
		}

		return (numerator + divisor - 1) / divisor;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 3;", 10, 4),
		Create("return 5;", 20, 4),
		Create("return 0;", 10, 0),
		Create("return (numerator + 4) / 5;", Unknown, 5),
		Create("return 0;", Unknown, 0)
	];
}
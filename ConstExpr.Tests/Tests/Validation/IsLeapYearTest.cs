using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsLeapYearTest() : BaseTest<Func<int, bool>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(year =>
	{
		if (year % 4 != 0)
		{
			return false;
		}

		if (year % 100 != 0)
		{
			return true;
		}

		return year % 400 == 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(year =>
		{
			var cast_val = (int) (year * 1374389535L >> 32);
			var rshift_2 = year >> 31;

			return (year & 3) == 0 && year - ((cast_val >> 5) - rshift_2) * 100 != 0 && year - ((cast_val >> 7) - rshift_2) * 400 == 0;
		}),
		Create(_ => true, [ 2000 ]),
		Create(_ => false, [ 1900 ])
	];
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsLeapYearTest() : BaseTestWithRandomValues<Func<int, bool>>(FastMathFlags.All | FastMathFlags.MagicNumberDivision, optimizations: OptimizationFlags.All)
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
			var castVal = (int) (year * 1374389535L >> 32);
			var rshift = year >> 31;

			return (year & 3) == 0 && year - ((castVal >> 5) - rshift) * 100 != 0 && year - ((castVal >> 7) - rshift) * 400 == 0;
		}),
	];
}
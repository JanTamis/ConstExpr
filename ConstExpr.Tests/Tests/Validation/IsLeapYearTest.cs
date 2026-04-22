using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsLeapYearTest() : BaseTest<Func<int, bool>>(FastMathFlags.FastMath)
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
		Create("""
			if ((year & 3) != 0)
			{
				return false;
			}
			
			return year % 100 != 0 && year % 400 == 0;
			"""),
		Create("return true;", 2000),
		Create("return false;", 1900)
	];
}
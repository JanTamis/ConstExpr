using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsLeapYearTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return true;", 2000),
		Create("return false;", 1900),
	];

	public override string TestMethod => """
		bool IsLeapYear(int year)
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
		}
		""";
}


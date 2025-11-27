namespace ConstExpr.Tests.Validation;

public class IsLeapYearTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (year % 4 != 0)
    {
    	return false;
    }

    if (year % 100 != 0)
    {
    	return true;
    }

    return year % 400 == 0;
    """,
    "return true;",
    "return false;"
  ];

  public override string Invocations => """
    var varYear = 2024;
    
    TestMethods.IsLeapYear(2000);
    TestMethods.IsLeapYear(1900);
    TestMethods.IsLeapYear(varYear);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static bool IsLeapYear(int year)
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


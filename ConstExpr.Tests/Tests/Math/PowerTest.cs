namespace ConstExpr.Tests.Math;

public class PowerTest : BaseTest
{
  public override IEnumerable<string> Result => 
  [
		"""
    if (exponent < 0)
    {
    	return 0L;
    }

    if (exponent == 0)
    {
    	return 1L;
    }

    var result = 1L;
    var base64 = (long)baseNum;

    while (exponent > 0)
    {
    	if (Int32.IsOddInteger(exponent))
    	{
    		result *= base64;
    	}

    	base64 *= base64;
    	exponent = (exponent + (exponent >> 31)) >> 1;
    }

    return result;
    """,
    "return 32L;",
    "return 1L;",
    "return 0L;",
    "return 1024L;"
  ];

  public override string Invocations => """
    var varBase = 3;
    var varExp = 4;
    
    TestMethods.Power(2, 5);
    TestMethods.Power(5, 0);
    TestMethods.Power(2, -3);
    TestMethods.Power(2, 10);
    TestMethods.Power(varBase, varExp);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static long Power(int baseNum, int exponent)
    {
      if (exponent < 0)
      {
        return 0;
      }
      if (exponent == 0)
      {
        return 1;
      }

      var result = 1L;
      var base64 = (long)baseNum;

      while (exponent > 0)
      {
        if (exponent % 2 == 1)
        {
          result *= base64;
        }
        base64 *= base64;
        exponent /= 2;
      }

      return result;
    }
    """;
}

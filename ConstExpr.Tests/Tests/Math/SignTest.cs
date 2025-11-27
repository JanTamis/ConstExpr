namespace ConstExpr.Tests.Math;

public class SignTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (n > 0)
    {
    	return 1;
    }

    if (n < 0)
    {
    	return -1;
    }

    return 0;
    """,
    "return 1;",
    "return -1;",
    "return 0;"
  ];

  public override string Invocations => """
    var varNum = -42;
    
    TestMethods.Sign(100);
    TestMethods.Sign(-50);
    TestMethods.Sign(0);
    TestMethods.Sign(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static int Sign(int n)
    {
      if (n > 0)
      {
        return 1;
      }
      if (n < 0)
      {
        return -1;
      }
      return 0;
    }
    """;
}


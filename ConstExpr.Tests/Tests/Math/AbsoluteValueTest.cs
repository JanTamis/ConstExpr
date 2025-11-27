namespace ConstExpr.Tests.Math;

public class AbsoluteValueTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
     """
     if (n < 0)
     {
     	return -n;
     }

     return n;
     """,
    "return 42;",
    "return 10;",
    "return 0;"
  ];

  public override string Invocations => """
    var varNum = -5;
    
    TestMethods.AbsoluteValue(-42);
    TestMethods.AbsoluteValue(10);
    TestMethods.AbsoluteValue(0);
    TestMethods.AbsoluteValue(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static int AbsoluteValue(int n)
    {
      if (n < 0)
      {
        return -n;
      }
      return n;
    }
    """;
}


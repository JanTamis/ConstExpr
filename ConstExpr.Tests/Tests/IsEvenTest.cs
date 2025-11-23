namespace ConstExpr.Tests;

public class IsEvenTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (n < 0)
    {
    	n = -n;
    }
    return (n & 1) == 0;
    """,
    "return true;",
    "return false;",
  ];

  public override string Invocations => """
    var local = 10;
    TestMethods.IsEven(4); // true
    TestMethods.IsEven(5); // false
    TestMethods.IsEven(local); // true
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static bool IsEven(int n)
    {
      if (n < 0) { n = -n; }
      return (n & 1) == 0;
    }
    """;
}


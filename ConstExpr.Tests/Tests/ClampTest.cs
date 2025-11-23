namespace ConstExpr.Tests;

public class ClampTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (value < 0)
    {
    	return 0;
    }
    if (value > 10)
    {
    	return 10;
    }
    return value;
    """,
    "return 5;",
    "return 0;",
    "return 10;"
  ];

  public override string Invocations => """
    var vx = 7; // treated as constant by generator
    TestMethods.Clamp(5, 0, 10);
    TestMethods.Clamp(-5, 0, 10);
    TestMethods.Clamp(15, 0, 10);
    TestMethods.Clamp(vx, 0, 10);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int Clamp(int value, int min, int max)
    {
      if (value < min)
      {
        return min;
      }
      if (value > max)
      {
        return max;
      }
      return value;
    }
    """;
}

namespace ConstExpr.Tests;

public class HypotenuseTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    "return 5D;",
    "return 13D;",
    "return 10D;"
  ];

  public override string Invocations => """
    var localA = 6;
    var localB = 8;
    TestMethods.Hypotenuse(3,4); // 5
    TestMethods.Hypotenuse(5,12); // 13
    TestMethods.Hypotenuse(localA, localB); // 10
    TestMethods.Hypotenuse(0,10); // 10
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.Strict)]
    public static double Hypotenuse(int a, int b)
    {
      return Math.Sqrt(a * a + b * b);
    }
    """;
}

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MaxOfTwoTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    "return 10;",
    "return 20;",
    "return 0;"
  ];

  public override string Invocations => """
    var x = 100;
    var y = 200;
    TestMethods.MaxOfTwo(5, 10);      // 10
    TestMethods.MaxOfTwo(-10, 20);    // 20
    TestMethods.MaxOfTwo(0, 0);       // 0
    TestMethods.MaxOfTwo(x, y);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static int MaxOfTwo(int a, int b)
    {
      return a > b ? a : b;
    }
    """;
}


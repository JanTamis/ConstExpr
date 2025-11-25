namespace ConstExpr.Tests.Math;

public class CubeTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    "return 125;",
    "return 0;",
    "return -8;"
  ];

  public override string Invocations => """
    var varN = 99;
    TestMethods.Cube(5);      // 125
    TestMethods.Cube(0);      // 0
    TestMethods.Cube(-2);     // -8
    TestMethods.Cube(varN);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static int Cube(int n)
    {
      return n * n * n;
    }
    """;
}


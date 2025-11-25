namespace ConstExpr.Tests.Math;

public class PercentageTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    "return value * percentage * 0.01;",
    "return 25.0;",
    "return 0.0;",
    "return 7.5;"
  ];

  public override string Invocations => """
    var varVal = 100.0;
    var varPct = 15.0;
    TestMethods.Percentage(100.0, 25.0);  // 25.0
    TestMethods.Percentage(50.0, 0.0);    // 0.0
    TestMethods.Percentage(50.0, 15.0);   // 7.5
    TestMethods.Percentage(varVal, varPct);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static double Percentage(double value, double percentage)
    {
      return value * percentage / 100;
    }
    """;
}


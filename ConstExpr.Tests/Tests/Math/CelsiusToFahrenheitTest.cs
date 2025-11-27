namespace ConstExpr.Tests.Math;

public class CelsiusToFahrenheitTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    "return 32;",
    "return 212;",
    "return 77;"
  ];

  public override string Invocations => """
    TestMethods.CelsiusToFahrenheit(0);
    TestMethods.CelsiusToFahrenheit(100);
    TestMethods.CelsiusToFahrenheit(25);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static double CelsiusToFahrenheit(double celsius)
    {
      return celsius * 9 / 5 + 32;
    }
    """;
}


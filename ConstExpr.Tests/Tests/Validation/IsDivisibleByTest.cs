namespace ConstExpr.Tests.Validation;

public class IsDivisibleByTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"return divisor != 0 && n % divisor == 0;",
    "return true;",
    "return false;"
  ];

  public override string Invocations => """
    var varN = 100;
    var varD = 7;
    TestMethods.IsDivisibleBy(10, 5);    // true
    TestMethods.IsDivisibleBy(10, 3);    // false
    TestMethods.IsDivisibleBy(0, 0);     // false
    TestMethods.IsDivisibleBy(varN, varD);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static bool IsDivisibleBy(int n, int divisor)
    {
      return divisor != 0 && n % divisor == 0;
    }
    """;
}



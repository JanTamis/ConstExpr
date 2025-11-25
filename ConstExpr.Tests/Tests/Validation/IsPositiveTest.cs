namespace ConstExpr.Tests.Validation;

public class IsPositiveTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    // "return n > 0;",
    "return true;",
    "return false;"
  ];

  public override string Invocations => """
    var varNum = -5;
    TestMethods.IsPositive(42);    // true
    TestMethods.IsPositive(-10);   // false
    TestMethods.IsPositive(0);     // false
    TestMethods.IsPositive(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static bool IsPositive(int n)
    {
      return n > 0;
    }
    """;
}



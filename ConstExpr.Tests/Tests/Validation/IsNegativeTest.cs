namespace ConstExpr.Tests.Validation;

public class IsNegativeTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"return n < 0;",
    "return true;",
    "return false;"
  ];

  public override string Invocations => """
    var varNum = 5;
    TestMethods.IsNegative(-10);   // true
    TestMethods.IsNegative(42);    // false
    TestMethods.IsNegative(0);     // false
    TestMethods.IsNegative(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static bool IsNegative(int n)
    {
      return n < 0;
    }
    """;
}


namespace ConstExpr.Tests.Validation;

public class IsMultipleOfTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    // "return divisor != 0 && n % divisor == 0;",
    "return true;",
    "return false;",
  ];

  public override string Invocations => """
    var varN = 100;
    var varD = 7;
    TestMethods.IsMultipleOf(15, 5);    // true
    TestMethods.IsMultipleOf(17, 3);    // false
    TestMethods.IsMultipleOf(0, 5);     // true (0 is multiple of any number)
    TestMethods.IsMultipleOf(varN, varD);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static bool IsMultipleOf(int n, int divisor)
    {
      return divisor != 0 && n % divisor == 0;
    }
    """;
}


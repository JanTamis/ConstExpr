namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsInRangeTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    "return value is >= 0 and <= 100;",
    "return true;",
    "return false;"
  ];

  public override string Invocations => """
    var varVal = 50;
    TestMethods.IsInRange(15, 1, 10);    // false
    TestMethods.IsInRange(1, 1, 10);     // true (inclusive)
    TestMethods.IsInRange(varVal, 0, 100);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static bool IsInRange(int value, int min, int max)
    {
      return value >= min && value <= max;
    }
    """;
}



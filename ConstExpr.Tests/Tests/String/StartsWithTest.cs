namespace ConstExpr.Tests.String;

public class StartsWithTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    // "return s.StartsWith(prefix);",
    "return true;",
    "return false;",
  ];

  public override string Invocations => """
    var varStr = "test";
    var varPre = "te";
    TestMethods.StartsWith("hello", "hel");   // true
    TestMethods.StartsWith("world", "foo");   // false
    TestMethods.StartsWith("", "");           // true
    TestMethods.StartsWith(varStr, varPre);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static bool StartsWith(string s, string prefix)
    {
      return s.StartsWith(prefix);
    }
    """;
}


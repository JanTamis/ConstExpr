namespace ConstExpr.Tests.String;

[InheritsTests]
public class ToUpperCaseTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    "return \"HELLO\";",
    "return \"WORLD123\";",
    "return \"\";"
  ];

  public override string Invocations => """
    var varStr = "test";
    TestMethods.ToUpperCase("hello");      // "HELLO"
    TestMethods.ToUpperCase("WoRlD123");   // "WORLD123"
    TestMethods.ToUpperCase("");           // ""
    TestMethods.ToUpperCase(varStr);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static string ToUpperCase(string s)
    {
      return s.ToUpper();
    }
    """;
}


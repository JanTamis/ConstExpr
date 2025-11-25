namespace ConstExpr.Tests.String;

public class ConcatenateTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    // "return a + b;",
    "return \"helloworld\";",
    "return \"test\";",
    "return \"\";"
  ];

  public override string Invocations => """
    var varA = "foo";
    var varB = "bar";
    TestMethods.Concatenate("hello", "world");  // "helloworld"
    TestMethods.Concatenate("test", "");        // "test"
    TestMethods.Concatenate("", "");            // ""
    TestMethods.Concatenate(varA, varB);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static string Concatenate(string a, string b)
    {
      return a + b;
    }
    """;
}


namespace ConstExpr.Tests.String;

public class StringLengthTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (s is null)
    {
    	return -1;
    }
    return s.Length;
    """,
    "return 0;",
    "return -1;",
    "return 11;"
  ];

  public override string Invocations => """
    var local = "hello";
    TestMethods.StringLength(""); // 0
    TestMethods.StringLength(local); // 5
    TestMethods.StringLength("hello world"); // 11
    TestMethods.StringLength((string?)null); // -1 generic
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static int StringLength(string s)
    {
      if (s is null)
      {
        return -1;
      }
      return s.Length;
    }
    """;
}


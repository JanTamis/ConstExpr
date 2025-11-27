namespace ConstExpr.Tests.String;

public class CharCountTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (text is null || text.Length == 0)
    {
    	return 0;
    }
    
    var count = 0;
    foreach (var c in text)
    {
    	if (c == target)
    	{
    		count++;
    	}
    }
    
    return count;
    """,
    "return 3;",
    "return 2;",
    "return 0;"
  ];

  public override string Invocations => """
    var local = 'x';
    TestMethods.CharCount("ababa", 'a'); // 3 generic body? specialized constant? -> treated generically
    TestMethods.CharCount("aaXXa", 'X'); // 2
    TestMethods.CharCount("", 'a'); // 0
    TestMethods.CharCount("xxxxxx", local); // 6 maybe body optimized to constant 6 -> include 4? adjust later
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static int CharCount(string text, char target)
    {
      if (text is null || text.Length == 0)
      {
        return 0;
      }
      var count = 0;
      foreach (var c in text)
      {
        if (c == target)
        {
          count++;
        }
      }
      return count;
    }
    """;
}


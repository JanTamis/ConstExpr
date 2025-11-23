namespace ConstExpr.Tests;

public class SumRangeTest : BaseTest
{
  public override IEnumerable<string> Result => 
  [
    """
    if (start > end)
    {
    	var temp = start;
    	start = end;
    	end = temp;
    }

    var n = end - start + 1;
    return (long)n * (start + end) / 2;
    """,
    "return 55L;",
    "return 5050L;",
    "return 25L;"
  ];

  public override string Invocations => """
    var varStart = 5;
    var varEnd = 10;
    
    TestMethods.SumRange(1, 10);
    TestMethods.SumRange(1, 100);
    TestMethods.SumRange(3, 7);
    TestMethods.SumRange(varStart, varEnd);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static long SumRange(int start, int end)
    {
      if (start > end)
      {
        var temp = start;
        start = end;
        end = temp;
      }

      var n = end - start + 1;
      return (long)n * (start + end) / 2;
    }
    """;
}

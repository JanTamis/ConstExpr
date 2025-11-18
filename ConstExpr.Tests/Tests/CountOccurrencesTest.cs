namespace ConstExpr.Tests;

public class CountOccurrencesTest : BaseTest
{
  public override IEnumerable<string> Result => 
  [
    """
    var count = 0;

    foreach (var num in numbers)
    {
    	if (num == target)
    	{
    		count++;
    	}
    }

    return count;
    """,
    "return 3;",
    "return 0;",
    "return 2;"
  ];

  public override string Invocations => """
    var varTarget = 5;
    var varInt1 = 5;
    var varInt2 = 10;
    var varInt3 = 5;
    
    TestMethods.CountOccurrences(5, 5, 10, 5, 20, 5);
    TestMethods.CountOccurrences(100, 1, 2, 3, 4, 5);
    TestMethods.CountOccurrences(7, 7, 14, 21, 7);
    TestMethods.CountOccurrences(varTarget, varInt1, varInt2, varInt3);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int CountOccurrences(int target, params int[] numbers)
    {
      var count = 0;
      foreach (var num in numbers)
      {
        if (num == target)
        {
          count++;
        }
      }

      return count;
    }
    """;
}

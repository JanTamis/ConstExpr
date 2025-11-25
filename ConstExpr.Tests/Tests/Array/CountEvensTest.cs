namespace ConstExpr.Tests.Array;

public class CountEvensTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"""
    //var count = 0;
    //foreach (var num in arr)
    //{
    //	if (num % 2 == 0)
    //	{
    //		count++;
    //	}
    //}
    
    //return count;
    //""",
    "return 3;",
    "return 0;",
    "return 4;"
  ];

  public override string Invocations => """
    var varArr = new[] { 1, 2, 3 };
    TestMethods.CountEvens(new[] { 1, 2, 3, 4, 5, 6 });  // 3 evens
    TestMethods.CountEvens(new int[] { });                // 0
    TestMethods.CountEvens(new[] { 2, 4, 6, 8 });        // 4 evens
    TestMethods.CountEvens(varArr);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int CountEvens(int[] arr)
    {
      var count = 0;
      foreach (var num in arr)
      {
        if (num % 2 == 0)
        {
          count++;
        }
      }
      return count;
    }
    """;
}


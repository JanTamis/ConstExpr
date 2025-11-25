namespace ConstExpr.Tests.Array;

public class ArraySumTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    var sum = 0;
    foreach (var num in arr)
    {
    	sum += num;
    }
    
    return sum;
    """,
    "return 15;",
    "return 0;",
    "return 42;"
  ];

  public override string Invocations => """
    var varArr = new[] { 1, 2, 3 };
    TestMethods.ArraySum(new[] { 1, 2, 3, 4, 5 }); // 15
    TestMethods.ArraySum(new int[] { });            // 0
    TestMethods.ArraySum(new[] { 42 });             // 42
    TestMethods.ArraySum(varArr);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static int ArraySum(int[] arr)
    {
      var sum = 0;
      foreach (var num in arr)
      {
        sum += num;
      }
      return sum;
    }
    """;
}


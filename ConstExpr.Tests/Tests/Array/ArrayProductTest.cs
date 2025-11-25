namespace ConstExpr.Tests.Array;

public class ArrayProductTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    var product = 1;
    foreach (var num in arr)
    {
    	product *= num;
    }
    
    return product;
    """,
    "return 120;",
    "return 1;",
    "return 0;"
  ];

  public override string Invocations => """
    var varArr = new[] { 1, 2, 3 };
    TestMethods.ArrayProduct(new[] { 1, 2, 3, 4, 5 }); // 120
    TestMethods.ArrayProduct(new int[] { });            // 1
    TestMethods.ArrayProduct(new[] { 5, 0, 3 });        // 0
    TestMethods.ArrayProduct(varArr);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static int ArrayProduct(int[] arr)
    {
      var product = 1;
      foreach (var num in arr)
      {
        product *= num;
      }
      return product;
    }
    """;
}


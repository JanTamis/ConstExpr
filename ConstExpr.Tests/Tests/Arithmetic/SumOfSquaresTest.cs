namespace ConstExpr.Tests.Arithmetic;

public class SumOfSquaresTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (n <= 0)
    {
    	return 0;
    }
    var total = 0;
    for (var i = 1; i <= n; i++)
    {
    	total += i * i;
    }
    return total;
    """,
    "return 55;",
    "return 0;",
    "return 14;",
  ];

  public override string Invocations => """
    var local = 10;
    TestMethods.SumOfSquares(5); // 55
    TestMethods.SumOfSquares(0); // 0
    TestMethods.SumOfSquares(3); // 14
    TestMethods.SumOfSquares(local); // 385
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int SumOfSquares(int n)
    {
      if (n <= 0)
      {
        return 0;
      }
      var total = 0;
      for (var i = 1; i <= n; i++)
      {
        total += i * i;
      }
      return total;
    }
    """;
}


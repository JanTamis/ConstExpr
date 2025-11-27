namespace ConstExpr.Tests.Arithmetic;

public class DigitSumTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (n < 0)
    {
    	n = -n;
    }

    var sum = 0;
    while (n > 0)
    {
    	sum += n % 10;
    	n /= 10;
    }

    return sum;
    """,
    "return 6;",
    "return 10;",
    "return 0;"
  ];

  public override string Invocations => """
    var varNum = 789;
    
    TestMethods.DigitSum(123);
    TestMethods.DigitSum(1234);
    TestMethods.DigitSum(0);
    TestMethods.DigitSum(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int DigitSum(int n)
    {
      if (n < 0)
      {
        n = -n;
      }
      
      var sum = 0;
      while (n > 0)
      {
        sum += n % 10;
        n /= 10;
      }
      return sum;
    }
    """;
}


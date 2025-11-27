namespace ConstExpr.Tests.Arithmetic;

public class CountDigitsTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (n == 0)
    {
    	return 1;
    }

    if (n < 0)
    {
    	n = -n;
    }

    var count = 0;
    while (n > 0)
    {
    	count++;
    	n /= 10;
    }

    return count;
    """,
    "return 3;",
    "return 1;"
  ];

  public override string Invocations => """
    var varNum = 12345;
    
    TestMethods.CountDigits(123);
    TestMethods.CountDigits(0);
    TestMethods.CountDigits(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int CountDigits(int n)
    {
      if (n == 0)
      {
        return 1;
      }
      
      if (n < 0)
      {
        n = -n;
      }
      
      var count = 0;
      while (n > 0)
      {
        count++;
        n /= 10;
      }
      return count;
    }
    """;
}


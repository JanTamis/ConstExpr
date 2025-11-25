namespace ConstExpr.Tests.NumberTheory;

public class DigitCountTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (n == 0)
    {
    	return 1;
    }
    
    var count = 0;
    var num = Math.Abs(n);
    while (num > 0)
    {
    	count++;
    	num /= 10;
    }
    
    return count;
    """,
    "return 3;",
    "return 1;",
    "return 4;"
  ];

  public override string Invocations => """
    var varNum = 999;
    TestMethods.DigitCount(123);   // 3 digits
    TestMethods.DigitCount(0);     // 1 digit
    TestMethods.DigitCount(-1234); // 4 digits
    TestMethods.DigitCount(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int DigitCount(int n)
    {
      if (n == 0)
      {
        return 1;
      }
      
      var count = 0;
      var num = Math.Abs(n);
      
      while (num > 0)
      {
        count++;
        num /= 10;
      }
      
      return count;
    }
    """;
}


namespace ConstExpr.Tests.NumberTheory;

public class SumOfDivisorsTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"""
    //if (n <= 0)
    //{
    //	return 0;
    //}
    
    //var sum = 0;
    //var i = 1;
    //while (i <= n)
    //{
    //	if (n % i == 0)
    //	{
    //		sum += i;
    //	}
    
    //	i++;
    //}
    
    //return sum;
    //""",
    "return 28;",
    "return 1;",
    "return 0;"
  ];

  public override string Invocations => """
    var varNum = 100;
    TestMethods.SumOfDivisors(12);   // 1+2+3+4+6+12 = 28
    TestMethods.SumOfDivisors(1);    // 1
    TestMethods.SumOfDivisors(0);    // 0
    TestMethods.SumOfDivisors(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int SumOfDivisors(int n)
    {
      if (n <= 0)
      {
        return 0;
      }
      
      var sum = 0;
      var i = 1;
      
      while (i <= n)
      {
        if (n % i == 0)
        {
          sum += i;
        }
        i++;
      }
      
      return sum;
    }
    """;
}


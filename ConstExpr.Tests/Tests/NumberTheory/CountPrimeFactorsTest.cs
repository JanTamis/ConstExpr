namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class CountPrimeFactorsTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"""
    //var count = 0;
    //var num = Math.Abs(n);
    //var i = 2;
    //while (i * i <= num)
    //{
    //	while (num % i == 0)
    //	{
    //		count++;
    //		num /= i;
    //	}
    
    //	i++;
    //}
    
    //if (num > 1)
    //{
    //	count++;
    //}
    
    //return count;
    //""",
    "return 3;",
    "return 0;"
  ];

  public override string Invocations => """
    var varNum = 100;
    TestMethods.CountPrimeFactors(12);   // 2^2 * 3 = 3 factors
    TestMethods.CountPrimeFactors(30);   // 2 * 3 * 5 = 3 factors
    TestMethods.CountPrimeFactors(1);    // 0 factors
    TestMethods.CountPrimeFactors(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int CountPrimeFactors(int n)
    {
      var count = 0;
      var num = Math.Abs(n);
      var i = 2;
      
      while (i * i <= num)
      {
        while (num % i == 0)
        {
          count++;
          num /= i;
        }
        i++;
      }
      
      if (num > 1)
      {
        count++;
      }
      
      return count;
    }
    """;
}


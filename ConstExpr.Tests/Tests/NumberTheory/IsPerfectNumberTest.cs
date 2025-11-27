namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class IsPerfectNumberTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"""
    //if (n <= 1)
    //{
    //	return false;
    //}
    
    //var sum = 1;
    //var i = 2;
    //while (i * i <= n)
    //{
    //	if (n % i == 0)
    //	{
    //		sum += i;
    //		if (i * i != n)
    //		{
    //			sum += n / i;
    //		}
    //	}
    
    //	i++;
    //}
    
    //return sum == n;
    //""",
    "return true;",
    "return false;"
  ];

  public override string Invocations => """
    var varNum = 100;
    TestMethods.IsPerfectNumber(6);    // true (1+2+3=6)
    TestMethods.IsPerfectNumber(28);   // true (1+2+4+7+14=28)
    TestMethods.IsPerfectNumber(12);   // false
    TestMethods.IsPerfectNumber(1);    // false
    TestMethods.IsPerfectNumber(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static bool IsPerfectNumber(int n)
    {
      if (n <= 1)
      {
        return false;
      }
      
      var sum = 1;
      var i = 2;
      
      while (i * i <= n)
      {
        if (n % i == 0)
        {
          sum += i;
          if (i * i != n)
          {
            sum += n / i;
          }
        }
        i++;
      }
      
      return sum == n;
    }
    """;
}


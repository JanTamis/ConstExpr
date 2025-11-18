namespace ConstExpr.Tests;

public class IsPrimeTest : BaseTest
{
  public override IEnumerable<string> Result => 
  [
    """
    if (n <= 1)
    {
    	return false;
    }

    if (n <= 3)
    {
    	return true;
    }

    if (n % 2 == 0 || n % 3 == 0)
    {
    	return false;
    }

    for (var i = 5; i * i <= n; i += 6)
    {
    	if (n % i == 0 || n % (i + 2) == 0)
    	{
    		return false;
    	}
    }

    return true;
    """,
    "return true;",
    "return false;",
    "return true;",
    "return false;"
  ];

  public override string Invocations => """
    var varInt = 15;
    
    TestMethods.IsPrime(17);
    TestMethods.IsPrime(1);
    TestMethods.IsPrime(29);
    TestMethods.IsPrime(100);
    TestMethods.IsPrime(varInt);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static bool IsPrime(int n)
    {
      if (n <= 1)
      {
        return false;
      }
      if (n <= 3)
      {
        return true;
      }
      if (n % 2 == 0 || n % 3 == 0)
      {
        return false;
      }

      for (var i = 5; i * i <= n; i += 6)
      {
        if (n % i == 0 || n % (i + 2) == 0)
        {
          return false;
        }
      }

      return true;
    }
    """;
}

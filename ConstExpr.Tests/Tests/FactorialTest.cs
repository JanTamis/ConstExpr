namespace ConstExpr.Tests;

public class FactorialTest : BaseTest
{
  public override IEnumerable<string> Result => 
  [
    """
    if (n < 0)
    {
    	return -1;
    }
    
    if (n == 0 || n == 1)
    {
    	return 1;
    }
    
    var result = 1L;
    
    for (var i = 2; i <= n; i++)
    {
    	result *= i;
    }
    
    return result;
    """,
    "return 120L;",
    "return 1L;",
    "return -1L;",
    "return 3628800L;"
  ];

  public override string Invocations => """
    var varInt = 3;
    
    TestMethods.Factorial(5);
    TestMethods.Factorial(1);
    TestMethods.Factorial(-5);
    TestMethods.Factorial(10);
    TestMethods.Factorial(varInt);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static long Factorial(int n)
    {
      if (n < 0)
      {
        return -1;
      }
      if (n == 0 || n == 1)
      {
        return 1;
      }

      var result = 1L;
      for (var i = 2; i <= n; i++)
      {
        result *= i;
      }
      return result;
    }
    """;
}

namespace ConstExpr.Tests;

public class FibonacciTest : BaseTest
{
  public override IEnumerable<string> Result => 
  [
    """
    if (n <= 0)
    {
    	return 0L;
    }

    if (n == 1)
    {
    	return 1L;
    }

    var prev = 0L;
    var curr = 1L;

    for (var i = 2; i <= n; i++)
    {
    	var next = prev + curr;
    	prev = curr;
    	curr = next;
    }

    return curr;
    """,
    "return 5L;",
    "return 1L;",
    "return 0L;",
    "return 55L;"
  ];

  public override string Invocations => """
    var varInt = 8;
    
    TestMethods.Fibonacci(5);
    TestMethods.Fibonacci(1);
    TestMethods.Fibonacci(0);
    TestMethods.Fibonacci(10);
    TestMethods.Fibonacci(varInt);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static long Fibonacci(int n)
    {
      if (n <= 0)
      {
        return 0;
      }
      if (n == 1)
      {
        return 1;
      }

      var prev = 0L;
      var curr = 1L;

      for (var i = 2; i <= n; i++)
      {
        var next = prev + curr;
        prev = curr;
        curr = next;
      }

      return curr;
    }
    """;
}

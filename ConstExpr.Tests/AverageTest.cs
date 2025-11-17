namespace ConstExpr.Tests;

public class AverageTest : BaseTest
{
  public override IEnumerable<string> Result => 
  [
    """
    if (numbers.Length == 0)
    {
    	return 0D;
    }

    var sum = 0;

    foreach (var num in numbers)
    {
    	sum += num;
    }

    return (double)sum / numbers.Length;
    """,
    "return 15D;",
    "return 30D;"
  ];

  public override string Invocations => """
    var varInt = 10;
    var varInt2 = 5;
    var varInt3 = 20;

    TestMethods.Average(10, 20, 30, 40, 50);
    TestMethods.Average(5, 15, 25);
    TestMethods.Average(varInt, varInt2, varInt3);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static double Average(params int[] numbers)
    {
      if (numbers.Length == 0)
      {
        return 0.0;
      }
    
      var sum = 0;
    
      foreach (var num in numbers)
      {
        sum += num;
      }
    
      return (double)sum / numbers.Length;
    }
    """;
}

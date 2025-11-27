namespace ConstExpr.Tests.Array;

public class FindMaxTest : BaseTest
{
  public override IEnumerable<string> Result => 
  [
    """
    if (numbers.Length == 0)
    {
    	return 0;
    }

    var max = numbers[0];

    for (var i = 1; i < numbers.Length; i++)
    {
    	if (numbers[i] > max)
    	{
    		max = numbers[i];
    	}
    }

    return max;
    """,
    "return 50;",
    "return 100;",
    "return -5;"
  ];

  public override string Invocations => """
    var varInt1 = 10;
    var varInt2 = 20;
    var varInt3 = 30;
    
    TestMethods.FindMax(10, 20, 50, 30);
    TestMethods.FindMax(5, 15, 25, 100, 50);
    TestMethods.FindMax(-10, -20, -5, -30);
    TestMethods.FindMax(varInt1, varInt2, varInt3);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static int FindMax(params int[] numbers)
    {
      if (numbers.Length == 0)
      {
        return 0;
      }

      var max = numbers[0];
      for (var i = 1; i < numbers.Length; i++)
      {
        if (numbers[i] > max)
        {
          max = numbers[i];
        }
      }

      return max;
    }
    """;
}

namespace ConstExpr.Tests.Math;

public class MaxOfThreeTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    var max = a;
    if (b > a)
    {
    	max = b;
    }

    if (c > max)
    {
    	max = c;
    }

    return max;
    """,
    "return 10;",
    "return 5;"
  ];

  public override string Invocations => """
    var varA = 3;
    var varB = 7;
    var varC = 5;
    
    TestMethods.MaxOfThree(5, 10, 3);
    TestMethods.MaxOfThree(5, 5, 5);
    TestMethods.MaxOfThree(varA, varB, varC);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int MaxOfThree(int a, int b, int c)
    {
      var max = a;
      if (b > max)
      {
        max = b;
      }
      if (c > max)
      {
        max = c;
      }
      return max;
    }
    """;
}


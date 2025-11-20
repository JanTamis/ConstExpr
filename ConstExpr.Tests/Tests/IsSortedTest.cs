namespace ConstExpr.Tests;

public class IsSortedTest : BaseTest
{
  public override IEnumerable<string> Result => 
  [
    """
    if (numbers.Length <= 1)
    {
    	return true;
    }

    for (var i = 1; i < numbers.Length; i++)
    {
    	if (numbers[i] < numbers[i - 1])
    	{
    		return false;
    	}
    }

    return true;
    """,
    "return true;",
    "return false;",
  ];

  public override string Invocations => """
    var varInt1 = 5;
    var varInt2 = 15;
    var varInt3 = 25;
    
    TestMethods.IsSorted(1, 2, 3, 4, 5);
    TestMethods.IsSorted(5, 3, 1, 2);
    TestMethods.IsSorted(10, 20, 30);
    TestMethods.IsSorted(varInt1, varInt2, varInt3);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static bool IsSorted(params int[] numbers)
    {
      if (numbers.Length <= 1)
      {
        return true;
      }

      for (var i = 1; i < numbers.Length; i++)
      {
        if (numbers[i] < numbers[i - 1])
        {
          return false;
        }
      }

      return true;
    }
    """;
}

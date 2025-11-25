namespace ConstExpr.Tests.Array;

public class ContainsElementTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"""
    //foreach (var item in arr)
    //{
    //	if (item == value)
    //	{
    //		return true;
    //	}
    //}
    
    //return false;
    //""",
    "return true;",
    "return false;"
  ];

  public override string Invocations => """
    var varArr = new[] { 1, 2, 3 };
    var varVal = 5;
    TestMethods.ContainsElement(new[] { 1, 2, 3, 4, 5 }, 3);  // true
    TestMethods.ContainsElement(new[] { 10, 20, 30 }, 5);     // false
    TestMethods.ContainsElement(varArr, varVal);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static bool ContainsElement(int[] arr, int value)
    {
      foreach (var item in arr)
      {
        if (item == value)
        {
          return true;
        }
      }
      return false;
    }
    """;
}


namespace ConstExpr.Tests.Array;

public class MinArrayTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (values.Length == 0)
    {
    	return 2147483647;
    }
    var min = 2147483647;
    foreach (var v in values)
    {
    	if (v < min) { min = v; }
    }
    return min;
    """,
    "return 3;",
    "return 1;",
    "return 2147483647;",
  ];

  public override string Invocations => """
    var local = new[]{42};
    TestMethods.MinArray(new[]{5,4,3,9}); // 3
    TestMethods.MinArray(new[]{7,2,1,8}); // 1
    TestMethods.MinArray(local); // 42
    TestMethods.MinArray([]); // int.MaxValue generic body
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static int MinArray(int[] values)
    {
      if (values.Length == 0)
      {
        return int.MaxValue;
      }
      var min = int.MaxValue;
      foreach (var v in values)
      {
        if (v < min)
        {
          min = v;
        }
      }
      return min;
    }
    """;
}


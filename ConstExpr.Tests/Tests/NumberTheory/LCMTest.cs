namespace ConstExpr.Tests.NumberTheory;

public class LCMTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (a == 0 || b == 0)
    {
    	return 0;
    }
    var aa = Math.Abs(a);
    var bb = Math.Abs(b);
    // compute gcd
    while (bb != 0)
    {
    	var temp = bb;
    	bb = aa % bb;
    	aa = temp;
    }
    var gcd = aa;
    return Math.Abs(a * b) / gcd;
    """,
    "return 12;",
    "return 0;",
    "return 42;",
  ];

  public override string Invocations => """
    var v1 = 8;
    var v2 = 12;
    TestMethods.LCM(4,6); // 12
    TestMethods.LCM(0,5); // 0
    TestMethods.LCM(21,6); // 42
    TestMethods.LCM(v1, v2); // 24
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int LCM(int a, int b)
    {
      if (a == 0 || b == 0)
      {
        return 0;
      }
      var aa = Math.Abs(a);
      var bb = Math.Abs(b);
      while (bb != 0)
      {
        var temp = bb;
        bb = aa % bb;
        aa = temp;
      }
      var gcd = aa;
      return Math.Abs(a * b) / gcd;
    }
    """;
}


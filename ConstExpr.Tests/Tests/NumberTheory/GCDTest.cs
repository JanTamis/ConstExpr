namespace ConstExpr.Tests.NumberTheory;

public class GCDTest : BaseTest
{
  public override IEnumerable<string> Result => 
  [
    """
    a = Math.Abs(a);
    b = Math.Abs(b);
    while (b != 0)
    {
    	var temp = b;
    	b = a % b;
    	a = temp;
    }

    return a;
    """,
    "return 6;",
    "return 1;",
    "return 15;"
  ];

  public override string Invocations => """
    var varInt1 = 20;
    var varInt2 = 30;
    
    TestMethods.GCD(48, 18);
    TestMethods.GCD(17, 19);
    TestMethods.GCD(45, 60);
    TestMethods.GCD(varInt1, varInt2);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int GCD(int a, int b)
    {
      a = Math.Abs(a);
      b = Math.Abs(b);

      while (b != 0)
      {
        var temp = b;
        b = a % b;
        a = temp;
      }

      return a;
    }
    """;
}

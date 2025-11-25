namespace ConstExpr.Tests.Math;

public class SwapTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"""
    //var temp = a;
    //a = b;
    //b = temp;
    //return (a, b);
    //""",
    "return (20, 10);",
    "return (0, 42);",
    "return (-5, 5);"
  ];

  public override string Invocations => """
    var varA = 100;
    var varB = 200;
    TestMethods.Swap(10, 20);    // (20, 10)
    TestMethods.Swap(42, 0);     // (0, 42)
    TestMethods.Swap(5, -5);     // (-5, 5)
    TestMethods.Swap(varA, varB);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static (int, int) Swap(int a, int b)
    {
      var temp = a;
      a = b;
      b = temp;
      return (a, b);
    }
    """;
}


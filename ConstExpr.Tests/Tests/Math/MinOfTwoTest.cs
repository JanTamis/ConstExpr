namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MinOfTwoTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    "return 5;",
    "return -10;",
    "return 0;"
  ];

  public override string Invocations => """
    var x = 100;
    var y = 200;
    TestMethods.MinOfTwo(5, 10);      // 5
    TestMethods.MinOfTwo(-10, 20);    // -10
    TestMethods.MinOfTwo(0, 0);       // 0
    TestMethods.MinOfTwo(x, y);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int MinOfTwo(int a, int b)
    {
      return a < b ? a : b;
    }
    """;
}



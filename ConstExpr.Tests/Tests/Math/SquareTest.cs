namespace ConstExpr.Tests.Math;

[InheritsTests]
public class SquareTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    "return 25;",
    "return 0;",
    "return 100;"
  ];

  public override string Invocations => """
    var varN = 99;
    TestMethods.Square(5);      // 25
    TestMethods.Square(0);      // 0
    TestMethods.Square(-10);    // 100
    TestMethods.Square(varN);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int Square(int n)
    {
      return n * n;
    }
    """;
}


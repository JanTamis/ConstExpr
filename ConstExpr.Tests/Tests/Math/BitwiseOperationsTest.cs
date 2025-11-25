namespace ConstExpr.Tests.Math;

public class BitwiseOperationsTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    "return a & b | a ^ b;",
    "return 14;",
    "return 8;",
    "return 5;"
  ];

  public override string Invocations => """
    var x = 10;
    var y = 20;
    TestMethods.BitwiseOr(12, 10); // 14
    TestMethods.BitwiseOr(8, 8);   // 8
    TestMethods.BitwiseOr(5, 0);   // 5
    TestMethods.BitwiseOr(x, y);   // non-constant
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int BitwiseOr(int a, int b)
    {
      return (a & b) | (a ^ b);
    }
    """;
}



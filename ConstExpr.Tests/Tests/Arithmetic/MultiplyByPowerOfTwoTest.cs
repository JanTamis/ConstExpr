namespace ConstExpr.Tests.Arithmetic;

public class MultiplyByPowerOfTwoTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    // "return n << power;",
    "return 40;",
    "return 0;",
    "return 128;"
  ];

  public override string Invocations => """
    var varN = 10;
    var varP = 2;
    TestMethods.MultiplyByPowerOfTwo(10, 2);  // 10 * 4 = 40
    TestMethods.MultiplyByPowerOfTwo(0, 5);   // 0
    TestMethods.MultiplyByPowerOfTwo(4, 5);   // 4 * 32 = 128
    TestMethods.MultiplyByPowerOfTwo(varN, varP);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int MultiplyByPowerOfTwo(int n, int power)
    {
      return n << power;
    }
    """;
}


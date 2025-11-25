namespace ConstExpr.Tests.Arithmetic;

public class DivideByPowerOfTwoTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    // "return n >> power;",
    "return 2;",
    "return 0;",
    "return 4;"
  ];

  public override string Invocations => """
    var varN = 100;
    var varP = 2;
    TestMethods.DivideByPowerOfTwo(10, 2);   // 10 / 4 = 2
    TestMethods.DivideByPowerOfTwo(1, 5);    // 1 / 32 = 0
    TestMethods.DivideByPowerOfTwo(128, 5);  // 128 / 32 = 4
    TestMethods.DivideByPowerOfTwo(varN, varP);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int DivideByPowerOfTwo(int n, int power)
    {
      return n >> power;
    }
    """;
}


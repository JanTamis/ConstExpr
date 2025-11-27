namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumOfFirstNTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"return n * (n + 1) / 2;",
    "return 55;",
    "return 0;",
    "return 5050;"
  ];

  public override string Invocations => """
    var varN = 200;
    TestMethods.SumOfFirstN(10);   // 1+2+...+10 = 55
    TestMethods.SumOfFirstN(0);    // 0
    TestMethods.SumOfFirstN(100);  // 5050
    TestMethods.SumOfFirstN(varN);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int SumOfFirstN(int n)
    {
      return n * (n + 1) / 2;
    }
    """;
}


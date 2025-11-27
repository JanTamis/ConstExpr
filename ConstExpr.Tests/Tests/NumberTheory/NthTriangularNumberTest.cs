namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class NthTriangularNumberTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"return n * (n + 1) / 2;",
    "return 15;",
    "return 1;",
    "return 55;"
  ];

  public override string Invocations => """
    var varN = 100;
    TestMethods.NthTriangularNumber(5);   // 15
    TestMethods.NthTriangularNumber(1);   // 1
    TestMethods.NthTriangularNumber(10);  // 55
    TestMethods.NthTriangularNumber(varN);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int NthTriangularNumber(int n)
    {
      return n * (n + 1) / 2;
    }
    """;
}


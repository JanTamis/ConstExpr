namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class CeilingDivisionTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"""
    //if (divisor == 0)
    //{
    //	return 0;
    //}
    
    //return (numerator + divisor - 1) / divisor;
    //""",
    "return 3;",
    "return 5;",
    "return 0;"
  ];

  public override string Invocations => """
    var varN = 100;
    var varD = 7;
    TestMethods.CeilingDivision(10, 4);   // 3 (ceiling of 2.5)
    TestMethods.CeilingDivision(20, 4);   // 5
    TestMethods.CeilingDivision(10, 0);   // 0 (division by zero guard)
    TestMethods.CeilingDivision(varN, varD);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int CeilingDivision(int numerator, int divisor)
    {
      if (divisor == 0)
      {
        return 0;
      }
      return (numerator + divisor - 1) / divisor;
    }
    """;
}


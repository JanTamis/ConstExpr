namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class CeilingDivisionTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
 [
    Create(null, Unknown, Unknown),
    Create("return 3;", 10, 4),
    Create("return 5;", 20, 4),
    Create("return 0;", 10, 0),
    Create("return (numerator + 4) / 5;", Unknown, 5),
    Create("return 0;", Unknown, 0),
  ];

  public override string TestMethod => """
    int CeilingDivision(int numerator, int divisor)
    {
      if (divisor == 0)
      {
        return 0;
      }

      return (numerator + divisor - 1) / divisor;
    }
    """;
}


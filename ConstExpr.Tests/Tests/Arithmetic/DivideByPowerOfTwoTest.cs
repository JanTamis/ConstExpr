namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class DivideByPowerOfTwoTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
 [
    Create(null, Unknown, Unknown),
		Create("return 2;", 10, 2),
    Create("return 0;", 1, 5),
    Create("return 4;", 128, 5),
	];

  public override string TestMethod => """
    int DivideByPowerOfTwo(int n, int power)
    {
      return n >> power;
    }
    """;
}


namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class CountDigitsTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
 [
    Create(null, Unknown),
    Create("return 3;", 123),
    Create("return 1;", 0),
    Create("return 4;", -4567),
	];

  public override string TestMethod => """
    int CountDigits(int n)
    {
      if (n == 0)
      {
        return 1;
      }
      
      if (n < 0)
      {
        n = -n;
      }
      
      var count = 0;

      while (n > 0)
      {
        count++;
        n /= 10;
      }

      return count;
    }
    """;
}


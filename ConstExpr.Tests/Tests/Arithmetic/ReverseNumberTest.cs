namespace ConstExpr.Tests.Arithmetic;

public class ReverseNumberTest : BaseTest
{
  public override IEnumerable<string> Result => 
  [
    """
    var originalN = n;
    n = Math.Abs(n);
    var reversed = 0;
    while (n > 0)
    {
    	reversed = reversed * 10 + n % 10;
    	n /= 10;
    }

    return Int32.CopySign(reversed, originalN);
    """,
    "return 321;",
    "return -654;",
    "return 1;",
  ];

  public override string Invocations => """
    var varInt = 987;
    TestMethods.ReverseNumber(123);
    TestMethods.ReverseNumber(-456);
    TestMethods.ReverseNumber(1);
    TestMethods.ReverseNumber(varInt);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int ReverseNumber(int n)
    {
      var originalN = n;
      n = Math.Abs(n);

      var reversed = 0;
      while (n > 0)
      {
        reversed = reversed * 10 + n % 10;
        n /= 10;
      }

      return Int32.CopySign(reversed, originalN);
    }
    """;
}

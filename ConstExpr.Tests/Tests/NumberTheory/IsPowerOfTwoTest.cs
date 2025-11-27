namespace ConstExpr.Tests.NumberTheory;

public class IsPowerOfTwoTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (n <= 0)
    {
    	return false;
    }

    return (n & (n - 1)) == 0;
    """,
    "return true;",
    "return false;"
  ];

  public override string Invocations => """
    var varNum = 64;
    
    TestMethods.IsPowerOfTwo(16);
    TestMethods.IsPowerOfTwo(18);
    TestMethods.IsPowerOfTwo(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static bool IsPowerOfTwo(int n)
    {
      if (n <= 0)
      {
        return false;
      }
      return (n & (n - 1)) == 0;
    }
    """;
}


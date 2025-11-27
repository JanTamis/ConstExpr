namespace ConstExpr.Tests.Validation;

public class IsPalindromeNumberTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (n < 0)
    {
    	return false;
    }

    var original = n;
    var reversed = 0;

    while (n > 0)
    {
    	reversed = reversed * 10 + n % 10;
    	n /= 10;
    }

    return original == reversed;
    """,
    "return true;",
    "return false;"
  ];

  public override string Invocations => """
    var varNum = 12321;
    
    TestMethods.IsPalindromeNumber(121);
    TestMethods.IsPalindromeNumber(123);
    TestMethods.IsPalindromeNumber(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static bool IsPalindromeNumber(int n)
    {
      if (n < 0)
      {
        return false;
      }
      
      var original = n;
      var reversed = 0;
      
      while (n > 0)
      {
        reversed = reversed * 10 + n % 10;
        n /= 10;
      }
      
      return original == reversed;
    }
    """;
}


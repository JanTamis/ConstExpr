namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class IsPalindromeTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"""
    //var original = Math.Abs(n);
    //var reversed = 0;
    //var temp = original;
    //while (temp > 0)
    //{
    //  reversed = reversed * 10 + temp % 10;
    //  temp /= 10;
    //}
    
    //return original == reversed;
    //""",
    "return true;",
    "return false;",
  ];

  public override string Invocations => """
    var varNum = 100;
    TestMethods.IsPalindrome(121);   // true
    TestMethods.IsPalindrome(123);   // false
    TestMethods.IsPalindrome(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static bool IsPalindrome(int n)
    {
      var original = Math.Abs(n);
      var reversed = 0;
      var temp = original;
      
      while (temp > 0)
      {
        reversed = reversed * 10 + temp % 10;
        temp /= 10;
      }
      
      return original == reversed;
    }
    """;
}


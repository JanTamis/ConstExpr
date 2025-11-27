namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class ProductOfDigitsTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"""
    //var product = 1;
    //var num = Math.Abs(n);
    //while (num > 0)
    //{
    //	product *= num % 10;
    //	num /= 10;
    //}
    
    //return product;
    //""",
    "return 24;",
    "return 0;",
    "return 5;"
  ];

  public override string Invocations => """
    var varNum = 999;
    TestMethods.ProductOfDigits(234);  // 2*3*4 = 24
    TestMethods.ProductOfDigits(105);  // 1*0*5 = 0
    TestMethods.ProductOfDigits(5);    // 5
    TestMethods.ProductOfDigits(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int ProductOfDigits(int n)
    {
      var product = 1;
      var num = Math.Abs(n);
      
      while (num > 0)
      {
        product *= num % 10;
        num /= 10;
      }
      
      return product;
    }
    """;
}


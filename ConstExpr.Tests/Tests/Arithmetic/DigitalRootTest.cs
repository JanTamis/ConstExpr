namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class DigitalRootTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"""
    //var num = Math.Abs(n);
    //while (num >= 10)
    //{
    //	var sum = 0;
    //	while (num > 0)
    //	{
    //		sum += num % 10;
    //		num /= 10;
    //	}
    
    //	num = sum;
    //}
    
    //return num;
    //""",
    "return 2;",
    "return 6;",
    "return 0;"
  ];

  public override string Invocations => """
    var varNum = 999;
    TestMethods.DigitalRoot(38);   // 3+8=11, 1+1=2... wait: 3+8=11, 1+1=2? No: 38->11->2
    TestMethods.DigitalRoot(942);  // 9+4+2=15, 1+5=6
    TestMethods.DigitalRoot(0);    // 0
    TestMethods.DigitalRoot(varNum);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int DigitalRoot(int n)
    {
      var num = Math.Abs(n);
      
      while (num >= 10)
      {
        var sum = 0;
        while (num > 0)
        {
          sum += num % 10;
          num /= 10;
        }
        num = sum;
      }
      
      return num;
    }
    """;
}


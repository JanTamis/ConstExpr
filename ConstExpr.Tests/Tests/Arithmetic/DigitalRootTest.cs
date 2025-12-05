using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class DigitalRootTest () : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
  [
    Create("""
    var num = Int32.Abs(n);
    
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
    """, Unknown),
    Create("return 2;", 38),
    Create("return 6;", 942),
    Create("return 0;", 0),
  ];

  public override string TestMethod => """
    int DigitalRoot(int n)
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


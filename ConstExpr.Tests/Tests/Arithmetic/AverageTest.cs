using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class AverageTest () : BaseTest(FloatingPointEvaluationMode.FastMath)
{
  public override IEnumerable<KeyValuePair<string?, object?[]>> Result => 
  [
    Create(null, Unknown),
    Create("return 30D;", new[] { 10, 20, 30, 40, 50 }),
    Create("return 15D;", new[] { 5, 15, 25 }),
    Create("return 0D;", System.Array.Empty<int>()),
	];

  public override string TestMethod => """
    double Average(int[] numbers)
    {
      if (numbers.Length == 0)
      {
        return 0.0;
      }
    
      var sum = 0;
    
      foreach (var num in numbers)
      {
        sum += num;
      }
    
      return (double)sum / numbers.Length;
    }
    """;
}

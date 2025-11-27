namespace ConstExpr.Tests.Math;

public class ModuloTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    """
    if (divisor == 0)
    {
    	return 0;
    }

    var result = dividend % divisor;
    if (result < 0)
    {
    	result += divisor;
    }

    return result;
    """,
    "return 3;",
    "return 2;",
    "return 0;"
  ];

  public override string Invocations => """
    var varDiv = 17;
    var varDivisor = 5;
    
    TestMethods.Modulo(13, 10);
    TestMethods.Modulo(-8, 5);
    TestMethods.Modulo(10, 0);
    TestMethods.Modulo(varDiv, varDivisor);
    """;

  public override string TestMethod => """
    [ConstExpr(FloatingPointMode = FloatingPointEvaluationMode.FastMath)]
    public static int Modulo(int dividend, int divisor)
    {
      if (divisor == 0)
      {
        return 0;
      }
      
      var result = dividend % divisor;
      if (result < 0)
      {
        result += divisor;
      }
      return result;
    }
    """;
}


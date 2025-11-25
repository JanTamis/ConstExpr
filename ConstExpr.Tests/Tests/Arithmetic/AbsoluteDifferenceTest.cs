namespace ConstExpr.Tests.Arithmetic;

public class AbsoluteDifferenceTest : BaseTest
{
  public override IEnumerable<string> Result =>
  [
    //"""
    //var diff = a - b;
    //return diff < 0 ? -diff : diff;
    //""",
    "return 5;",
    "return 30;",
    "return 0;"
  ];

  public override string Invocations => """
    var x = 100;
    var y = 200;
    TestMethods.AbsoluteDifference(10, 5);     // 5
    TestMethods.AbsoluteDifference(-10, 20);   // 30
    TestMethods.AbsoluteDifference(42, 42);    // 0
    TestMethods.AbsoluteDifference(x, y);
    """;

  public override string TestMethod => """
    [ConstExpr]
    public static int AbsoluteDifference(int a, int b)
    {
      var diff = a - b;
      return diff < 0 ? -diff : diff;
    }
    """;
}


using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class AbsoluteDifferenceTest () : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => """
    int AbsoluteDifference(int a, int b)
    {
      var diff = a - b;
      return diff < 0 ? -diff : diff;
    }
    """;

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 5;", 10, 5),
		Create("return 30;", -10, 20),
		Create("return 0;", 42, 42)
	];
}


using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class AbsoluteValueTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 42;", -42),
		Create("return 10;", 10),
		Create("return 0;", 0),
	];

	public override string TestMethod => """
		int AbsoluteValue(int n)
		{
			if (n < 0)
			{
				return -n;
			}
			return n;
		}
		""";
}


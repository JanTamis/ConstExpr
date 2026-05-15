using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MinOfTwoTest() : BaseTest<Func<int, int, int>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) => a < b ? a : b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return a < b ? a : b;"),
		Create("return 5;", 5, 10),
		Create("return -10;", -10, 20),
		Create("return 0;", 0, 0)
	];
}
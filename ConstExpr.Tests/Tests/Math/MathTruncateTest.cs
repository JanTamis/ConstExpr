using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathTruncateTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Truncate(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.Truncate(x);"),
		Create("return 3D;", 3.7),
		Create("return -3D;", -3.2),
	];
}
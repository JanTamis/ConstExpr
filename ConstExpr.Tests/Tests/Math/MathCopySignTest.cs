using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathCopySignTest() : BaseTest<Func<double, double, double>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((x, y) => System.Math.CopySign(x, y));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return CopySignFastDouble(x, y);"),
		Create("return double.Abs(x);", Unknown, 2.0),
		Create("return -double.Abs(x);", Unknown, -2.0),
	];
}
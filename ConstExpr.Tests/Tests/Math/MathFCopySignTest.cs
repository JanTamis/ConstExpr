using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFCopySignTest() : BaseTest<Func<float, float, float>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((x, y) => MathF.CopySign(x, y));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return CopySignFastFloat(x, y);"),
		Create("return float.Abs(x);", Unknown, 2f),
		Create("return -float.Abs(x);", Unknown, -2f),
	];
}
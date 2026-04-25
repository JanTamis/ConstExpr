using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathCopySignTest() : BaseTest<Func<double, double, double>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((x, y) => System.Math.CopySign(x, y));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return CopySignFastDouble(x, y);"),
		Create("return double.Abs(x);", Unknown, 2.0),
		Create("return -double.Abs(x);", Unknown, -2.0),
	];
}

[InheritsTests]
public class MathFCopySignTest() : BaseTest<Func<float, float, float>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((x, y) => System.MathF.CopySign(x, y));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return CopySignFastFloat(x, y);"),
		Create("return float.Abs(x);", Unknown, 2f),
		Create("return -float.Abs(x);", Unknown, -2f),
	];
}


using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathILogBTest() : BaseTest<Func<double, int>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.Math.ILogB(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.ILogB(x);"),
		Create("return 3;", 8.0),
	];
}

[InheritsTests]
public class MathFILogBTest() : BaseTest<Func<float, int>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(x => System.MathF.ILogB(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return float.ILogB(x);"),
		Create("return 3;", 8f),
	];
}


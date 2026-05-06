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
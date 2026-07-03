using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathILogBTest() : BaseTest<Func<double, int>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(x => System.Math.ILogB(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.ILogB(x)),
		Create(_ => 3, [ 8.0 ])
	];
}
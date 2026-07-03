using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class MathFBitDecrementTest() : BaseTest<Func<float, float>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(x => MathF.BitDecrement(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastBitDecrement(x);"),
		Create(_ => 1.9999999F, [ 2f ])
	];
}
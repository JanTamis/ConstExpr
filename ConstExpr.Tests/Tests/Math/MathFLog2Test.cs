using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.Log2(float) -> FastLog2(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFLog2Test() : BaseTest<Func<float, float>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => MathF.Log2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLog2(x);"),
	];
}
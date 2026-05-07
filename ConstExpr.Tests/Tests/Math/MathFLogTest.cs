using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>MathF.Log(float) → FastLog(x) in FastMath mode.</summary>
[InheritsTests]
public class MathFLogTest() : BaseTest<Func<float, float>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => MathF.Log(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLog(x);"),
	];
}
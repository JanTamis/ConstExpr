using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Asinh(double) -> FastAsinh(x) in FastMath mode, constant-folds when input is known.</summary>
[InheritsTests]
public class MathAsinhTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Asinh(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAsinh(x);"),
		Create("return 0D;", 0.0),
	];
}
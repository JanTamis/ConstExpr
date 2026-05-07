using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Atanh(double) -> FastAtanh(x) in FastMath mode, constant-folds when input is known.</summary>
[InheritsTests]
public class MathAtanhTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Atanh(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAtanh(x);"),
		Create("return 0D;", 0.0),
	];
}
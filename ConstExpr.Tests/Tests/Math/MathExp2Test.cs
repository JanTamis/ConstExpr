using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Exp2(double) → FastExp2(x) in FastMath mode, constant-folds when input is known.</summary>
[InheritsTests]
public class MathExp2Test() : BaseTest<Func<double, double>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => double.Exp2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastExp2(x);"),
		Create(_ => 1D, [ 0.0 ]),
		Create(_ => 8D, [ 3.0 ]),
	];
}
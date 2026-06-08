using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Tan(double) → FastTan(x) in FastMath mode, with algebraic constant folding.</summary>
[InheritsTests]
public class MathTanTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Tan(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastTan(x);"),
		Create(_ => 0D, [ 0.0 ]),
	];
}
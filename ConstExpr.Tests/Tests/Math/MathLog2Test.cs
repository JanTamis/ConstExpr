using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Log2(double) -> FastLog2(x) in FastMath mode, constant-folds when input is known.</summary>
[InheritsTests]
public class MathLog2Test() : BaseTest<Func<double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Log2(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastLog2(x);"),
		Create(_ => 0D, [ 1.0 ]),
		Create(_ => 3D, [ 8.0 ]),
	];
}
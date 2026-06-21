using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Math.Acosh(double) -> FastAcosh(x) in FastMath mode, constant-folds when input is known.</summary>
[InheritsTests]
public class MathAcoshTest() : BaseTest<Func<double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Acosh(x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return FastAcosh(x);"),
		Create(_ => 0D, [ 1.0 ])
	];
}
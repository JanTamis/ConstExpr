using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Sqrt(x * x) → Abs(x): algebraic identity for pure expressions.</summary>
[InheritsTests]
public class MathSqrtAlgebraicTest() : BaseTest<Func<double, double>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Sqrt(x * x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => double.Abs(x)),
	];
}
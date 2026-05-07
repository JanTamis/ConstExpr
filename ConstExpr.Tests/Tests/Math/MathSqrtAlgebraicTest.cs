using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Sqrt(x * x) → Abs(x): algebraic identity for pure expressions.</summary>
[InheritsTests]
public class MathSqrtAlgebraicTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Sqrt(x * x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return double.Abs(x);"),
	];
}
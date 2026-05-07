using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Truncate(-x) → -(Truncate(x)): moves negation outside.</summary>
[InheritsTests]
public class MathTruncateNegationTest() : BaseTest<Func<double, double>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(x => System.Math.Truncate(-x));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return -Double.Truncate(x);"),
	];
}
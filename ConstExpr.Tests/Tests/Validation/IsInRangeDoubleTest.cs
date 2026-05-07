using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsInRangeDoubleTest() : BaseTest<Func<double, double, double, bool>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((value, min, max) => value >= min && value <= max);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return Double.Abs(value - 5.5) <= 4.5;", Unknown, 1D, 10D),
		Create("return false;", Unknown, 10D, 1D),
		Create("return false;", Unknown, -1D, -10D),
		Create("return Double.Abs(value + 5.5) <= 4.5;", Unknown, -10D, -1D),
		Create("return false;", 15D, 1D, 10D),
		Create("return true;", 1D, 1D, 10D)
	];
}
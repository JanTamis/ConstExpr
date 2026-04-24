using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsInRangeTest() : BaseTest<Func<int, int, int, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((value, min, max) => value >= min && value <= max);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown, Unknown, Unknown),
		Create("return (uint)(value - 1) <= 9U;", Unknown, 1, 10),
		Create("return false", Unknown, 10, 1),
		Create("return false", Unknown, -1, -10),
		Create("return (uint)(value + 10) <= 9U;", Unknown, -10, -1),
		Create("return false;", 15, 1, 10),
		Create("return true;", 1, 1, 10)
	];
}

[InheritsTests]
public class IsInRangeDoubleTest() : BaseTest<Func<double, double, double, bool>>(FastMathFlags.FastMath)
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
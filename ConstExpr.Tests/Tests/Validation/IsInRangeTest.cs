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
		Create("return (uint)(value + 10) <= 9u;", Unknown, -10, -1),
		Create("return false;", 15, 1, 10),
		Create("return true;", 1, 1, 10)
	];
}
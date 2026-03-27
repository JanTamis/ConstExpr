using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsInRangeTest() : BaseTest<Func<int, int, int, bool>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((value, min, max) => value >= min && value <= max);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown, Unknown, Unknown),
		Create("return (uint)(value - 1) < 9U;", Unknown, 1, 10),
		Create("return false", Unknown, 10, 1),
		Create("return false", Unknown, -1, -10),
		Create("return value is >= -10 and <= -1;", Unknown, -10, -1),
		Create("return false;", 15, 1, 10),
		Create("return true;", 1, 1, 10)
	];
}
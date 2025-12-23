using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsInRangeTest() : BaseTest<Func<int, int, int, bool>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((value, min, max) => value >= min && value <= max);

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown, Unknown),
		Create("return false;", 15, 1, 10),
		Create("return true;", 1, 1, 10)
	];
}
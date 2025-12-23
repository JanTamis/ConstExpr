using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsPositiveTest() : BaseTest<Func<int, bool>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n => n > 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return true;", 42),
		Create("return false;", -10),
		Create("return false;", 0)
	];
}
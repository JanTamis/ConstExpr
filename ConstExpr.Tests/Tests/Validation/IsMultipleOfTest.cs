using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsMultipleOfTest() : BaseTest<Func<int, int, bool>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((n, divisor) => divisor != 0 && n % divisor == 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return true;", 15, 5),
		Create("return false;", 17, 3),
		Create("return true;", 0, 5)
	];
}
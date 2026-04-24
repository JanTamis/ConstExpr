using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsDivisibleByTest() : BaseTest<Func<int, int, bool>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((n, divisor) => divisor != 0 && n % divisor == 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return true;", 10, 5),
		Create("return false;", 10, 3),
		Create("return false;", 0, 0)
	];
}
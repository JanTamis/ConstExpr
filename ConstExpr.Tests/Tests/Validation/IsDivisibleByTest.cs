namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsDivisibleByTest : BaseTest<Func<int, int, bool>>
{
	public override string TestMethod => GetString((n, divisor) => divisor != 0 && n % divisor == 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(10, 5),
		CreateFolded(10, 3),
		CreateFolded(0, 0)
	];
}
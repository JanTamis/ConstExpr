namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsNegativeTest : BaseTestWithRandomValues<Func<int, bool>>
{
	public override string TestMethod => GetString(n => n < 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
	];
}
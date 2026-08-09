namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsNegativeTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(n => n < 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(-10),
		CreateFolded(0)
	];
}
namespace ConstExpr.Tests.String;

[InheritsTests]
public class StringIsNullOrEmptyTest : BaseTestWithRandomValues<Func<string?, bool>>
{
	public override string TestMethod => GetString(s => System.String.IsNullOrEmpty(s));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(s => System.String.IsNullOrEmpty(s)),
	];
}
namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsNegativeTest : BaseTest<Func<int, bool>>
{
	public override string TestMethod => GetString(n => n < 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => true, [ -10 ]),
		Create(_ => false, [ 0 ])
	];
}
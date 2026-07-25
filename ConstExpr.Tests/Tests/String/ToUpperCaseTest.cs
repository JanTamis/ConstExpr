namespace ConstExpr.Tests.String;

[InheritsTests]
public class ToUpperCaseTest : BaseTest<Func<string, string>>
{
	public override string TestMethod => GetString(s => s.ToUpper());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => "HELLO", [ "hello" ]),
		Create(_ => "WORLD123", [ "WoRlD123" ]),
		Create(_ => "", [ System.String.Empty ])
	];
}
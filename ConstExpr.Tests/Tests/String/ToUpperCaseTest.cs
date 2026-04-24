using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ToUpperCaseTest() : BaseTest<Func<string, string>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(s => s.ToUpper());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return \"HELLO\";", "hello"),
		Create("return \"WORLD123\";", "WoRlD123"),
		Create("return \"\";", "")
	];
}
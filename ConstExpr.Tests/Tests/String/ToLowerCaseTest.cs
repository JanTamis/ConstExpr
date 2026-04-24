using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ToLowerCaseTest() : BaseTest<Func<string, string>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString(s => s.ToLower());

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create("return \"hello\";", "HELLO"),
		Create("return \"world123\";", "WoRlD123"),
		Create("return \"\";", ""),
	];
}


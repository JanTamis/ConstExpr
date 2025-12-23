using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ToUpperCaseTest() : BaseTest<Func<string, string>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(s => s.ToUpper());

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return \"HELLO\";", "hello"),
		Create("return \"WORLD123\";", "WoRlD123"),
		Create("return \"\";", "")
	];
}
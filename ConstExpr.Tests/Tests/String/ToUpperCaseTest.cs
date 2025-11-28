using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ToUpperCaseTest(FloatingPointEvaluationMode evaluationMode = FloatingPointEvaluationMode.FastMath) : BaseTest(evaluationMode)
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown),
		Create("return \"HELLO\";", "hello"),
		Create("return \"WORLD123\";", "WoRlD123"),
		Create("return \"\";", ""),
	];

	public override string TestMethod => """
		string ToUpperCase(string s)
		{
			return s.ToUpper();
		}
		""";
}


using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ConcatenateTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return \"helloworld\";", "hello", "world"),
		Create("return \"test\";", "test", ""),
		Create("return \"\";", "", ""),
	];

	public override string TestMethod => """
		string Concatenate(string a, string b)
		{
			return a + b;
		}
		""";
}


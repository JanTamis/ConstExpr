using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ConcatenateTest() : BaseTest<Func<string, string, string>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((a, b) => a + b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return \"helloworld\";", "hello", "world"),
		Create("return \"test\";", "test", ""),
		Create("return \"\";", "", "")
	];
}
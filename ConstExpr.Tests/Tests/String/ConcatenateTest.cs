using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class ConcatenateTest() : BaseTest<Func<string, string, string>>(FastMathFlags.FastMath)
{
	public override string TestMethod => GetString((a, b) => a + b);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown, Unknown),
		Create("return \"helloworld\";", "hello", "world"),
		Create("return \"test\";", "test", ""),
		Create("return \"\";", "", "")
	];
}
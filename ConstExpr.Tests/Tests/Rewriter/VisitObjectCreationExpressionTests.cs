namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitObjectCreationExpression - constant constructor evaluation
/// </summary>
[InheritsTests]
public class VisitObjectCreationExpressionTests : BaseTest
{
	public override string TestMethod => """
		(string, string) TestMethod(int amount, char[] chars)
		{
			var s1 = new string('a', amount);
			var s2 = new string(chars);
			
			return (s1, s2);
		}
	""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		// When values are unknown, keep the original code unchanged
		Create(null, Unknown, Unknown),
		// When values are known and constant, they get inlined into the return statement
		Create("return (\"aaaaa\", \"hello\");", 5, new[] { 'h', 'e', 'l', 'l', 'o' }),
		Create("return (\"\", \"\");", 0, new char[] {  } ),
		Create("return (\"aaa\", \"cat\");", 3, new[] { 'c', 'a', 't' } ),
		Create("return (\"\", \"xyz\");", 0, new[] { 'x', 'y', 'z' } )
	];
}


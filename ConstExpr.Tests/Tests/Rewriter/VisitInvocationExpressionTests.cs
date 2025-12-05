namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitInvocationExpression - constant method call evaluation
/// </summary>
[InheritsTests]
public class VisitInvocationExpressionTests : BaseTest
{
	public override string TestMethod => """
		(string, string, int) TestMethod()
		{
			string a = nameof(TestMethod);
			string b = string.Concat("hello", " ", "world");
			int c = Math.Abs(-42);
			return (a, b, c);
		}
	""";

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return (\"TestMethod\", \"hello world\", 42);")
	];
}

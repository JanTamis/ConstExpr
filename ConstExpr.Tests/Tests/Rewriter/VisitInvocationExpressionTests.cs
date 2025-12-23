namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitInvocationExpression - constant method call evaluation
/// </summary>
[InheritsTests]
public class VisitInvocationExpressionTests : BaseTest<Func<(string, string, int)>>
{
	public override string TestMethod => GetString(() =>
	{
		var a = nameof(TestMethod);
		var b = System.String.Concat("hello", " ", "world");
		var c = System.Math.Abs(-42);

		return (a, b, c);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return (\"TestMethod\", \"hello world\", 42);")
	];
}
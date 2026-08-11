namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitInterpolatedStringExpression - fold to string literal when all parts constant
/// </summary>
[InheritsTests]
public class VisitInterpolatedStringExpressionTests : BaseTestWithRandomValues<Func<int, (string, string)>>
{
	public override string TestMethod => GetString(x =>
	{
		var s = $"Value: {x}";
		var t = $"Hello {" world"}";

		return (s, t);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => ($"Value: {x}", "Hello  world")),
	];
}
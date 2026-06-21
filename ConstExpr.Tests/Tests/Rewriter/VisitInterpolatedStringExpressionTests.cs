namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitInterpolatedStringExpression - fold to string literal when all parts constant
/// </summary>
[InheritsTests]
public class VisitInterpolatedStringExpressionTests : BaseTest<Func<int, (string, string)>>
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
		Create(_ => ("Value: 42", "Hello  world"), [ 42 ]),
		Create(_ => ("Value: 50", "Hello  world"), [ 50 ])
	];
}
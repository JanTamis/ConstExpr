namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitPostfixUnaryExpression - ++ and -- folding
/// </summary>
[InheritsTests]
public class VisitPostfixUnaryExpressionTests : BaseTestWithRandomValues<Func<int, int>>
{
	public override string TestMethod => GetString(x =>
	{
		var a = x;
		var b = a++;
		var c = a--;

		return a;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x),
	];
}
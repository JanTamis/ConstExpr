namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitPostfixUnaryExpression - ++ and -- folding
/// </summary>
[InheritsTests]
public class VisitPostfixUnaryExpressionTests : BaseTest<Func<int, int>>
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
		Create(_ => 7, [ 7 ]),
		Create(_ => 1, [ 1 ]),
		Create(_ => -1, [ -1 ]),
		Create(_ => 0, [ 0 ])
	];
}
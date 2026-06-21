namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitSimpleLambdaExpression - lambda constant folding
/// </summary>
[InheritsTests]
public class VisitSimpleLambdaExpressionTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(y =>
	{
		var func = (int x) => x + 1;

		return func(y);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(y => y + 1),
		Create(_ => 7, [ 6 ]),
		Create(_ => 12, [ 11 ]),
		Create(_ => 2, [ 1 ]),
		Create(_ => 0, [ -1 ]),
		Create(_ => 1, [ 0 ])
	];
}
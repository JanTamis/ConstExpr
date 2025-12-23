namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitSimpleLambdaExpression - lambda constant folding
/// </summary>
[InheritsTests]
public class VisitSimpleLambdaExpressionTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(y =>
	{
		var func = (int x) => x + 1;

		return func(y);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 7;", 6),
		Create("return 12;", 11),
		Create("return 2;", 1),
		Create("return 0;", -1),
		Create("return 1;", 0)
	];
}
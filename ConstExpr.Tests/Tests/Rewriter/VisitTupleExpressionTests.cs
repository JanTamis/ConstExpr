namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitTupleExpression - fold to tuple literal or visit args
/// </summary>
[InheritsTests]
public class VisitTupleExpressionTests : BaseTestWithRandomValues<Func<int, int, string, ((int, int), (int, int), (int, string), (int, int))>>
{
	public override string TestMethod => GetString((x, y, s) =>
	{
		var t1 = (1, 2);
		var t2 = (1 + 2, 3 * 4);
		var t3 = (x, y);
		var t4 = (x + y, s);
		var t5 = ((x, y), t2);
		var t6 = (10 + 5, x * 2);

		return (t1, t2, t4, t6);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y, s) => ((1, 2), (3, 12), (x + y, s), (15, x << 1))),
	];
}
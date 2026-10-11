namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   A discarded call to a pure method (<c>data.Reverse()</c> binds to LINQ's non-mutating
///   <c>Enumerable.Reverse</c> here) folds to a collection literal. That literal must not be emitted as an
///   expression statement (<c>[3, 2, 1];</c> is CS0201) - the statement is dropped instead.
/// </summary>
[InheritsTests]
public class VisitExpressionStatementDiscardedPureCallTests : BaseTest<Func<int[], bool, int>>
{
	public override string TestMethod => GetString((data, flag) =>
	{
		if (flag)
		{
			data.Reverse();
		}

		return data[0];
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return 1;", new[] { 1, 2, 3 }, Unknown),
		Create("return 1;", new[] { 1, 2, 3 }, true),
		Create("return 1;", new[] { 1, 2, 3 }, false)
	];
}
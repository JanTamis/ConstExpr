namespace ConstExpr.Tests.Rewriter;

/// <summary>
/// Tests for VisitElementAccessExpression - array/indexer constant evaluation
/// </summary>
[InheritsTests]
public class VisitElementAccessExpressionTests : BaseTest<Func<int[], int, int, (int, int, int, int)>>
{
	public override string TestMethod => GetString((arr, index1, index2) =>
	{
		var a = arr[0];
		var b = arr[2];
		var c = arr[index1];
		var d = arr[index2];

		return (a, b, c, d);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((arr, index1, index2) => (arr[0], arr[2], arr[index1], arr[index2])),
		Create((_, _, _) => (10, 30, 10, 50), [ new[] { 10, 20, 30, 40, 50 }, 0, 4 ]),
		Create((_, _, _) => (5, 15, 15, 25), [ new[] { 5, 10, 15, 20, 25 }, 2, 4 ]),
		Create((_, _, _) => (100, 300, 200, 300), [ new[] { 100, 200, 300, 400, 500 }, 1, 2 ]),
		Create((_, _, _) => (1, 3, 5, 1), [ new[] { 1, 2, 3, 4, 5 }, 4, 0 ])
	];
}
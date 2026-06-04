namespace ConstExpr.Tests.Rewriter;

/// <summary>
/// Tests for VisitLocalFunctionStatement - process, inline const local functions
/// </summary>
[InheritsTests]
public class VisitLocalFunctionStatementTests : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(x =>
	{
		int Add(int a, int b) => a + b;

		return Add(x, 2);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x + 2),
		Create(_ => 3, [ 1 ]),
		Create(_ => 10, [ 8 ]),
		Create(_ => -5, [ -7 ]),
		Create(_ => 0, [ -2 ]),
		Create(_ => 42, [ 40 ])
	];
}
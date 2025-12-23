namespace ConstExpr.Tests.Tests.Rewriter;

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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("return Add(x, 2);", Unknown),
		Create("return 3;", 1),
		Create("return 10;", 8),
		Create("return -5;", -7),
		Create("return 0;", -2),
		Create("return 42;", 40)
	];
}
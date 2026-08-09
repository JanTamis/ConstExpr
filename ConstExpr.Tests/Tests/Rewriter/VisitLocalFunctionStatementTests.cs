namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitLocalFunctionStatement - process, inline const local functions
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
		CreateFolded(1),
		CreateFolded(8),
		CreateFolded(-7),
		CreateFolded(-2),
		CreateFolded(40)
	];
}
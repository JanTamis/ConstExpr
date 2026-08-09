namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitBlock - visits statements in block, handles nested scopes and variable folding
/// </summary>
[InheritsTests]
public class VisitBlockTests : BaseTest<Func<int, int, int>>
{
	public override string TestMethod => GetString((x, y) =>
	{
		int result;
		{
			var a = x + 10;
			var b = y * 2;
			result = a + b;
		}
		{
			var c = x - 5;
			result = result + c;
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) => (y << 1) + (x << 1) + 5),
		CreateFolded(5, 3),
		CreateFolded(10, 5),
		CreateFolded(0, 1),
		CreateFolded(-10, 10)
	];
}
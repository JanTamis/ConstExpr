namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitBlock - visits statements in block, handles nested scopes and variable folding
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var result;
			var a = x + 10;
			var b = y << 1;
			result = a + b;

			var c = x - 5;

			result = result + c;

			return result;
			""", Unknown, Unknown),
		Create("return 21;", 5, 3),
		Create("return 35;", 10, 5),
		Create("return 7;", 0, 1),
		Create("return 5;", -10, 10)
	];
}
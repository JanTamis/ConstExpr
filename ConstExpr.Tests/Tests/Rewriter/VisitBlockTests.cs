namespace ConstExpr.Tests.Rewriter;

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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((x, y) =>
		{
			var result = x + 10 + (y << 1);
			result += x - 5;

			return result;
		}),
		Create((_, _) => 21, [ 5, 3 ]),
		Create((_, _) => 35, [ 10, 5 ]),
		Create((_, _) => 7, [ 0, 1 ]),
		Create((_, _) => 5, [ -10, 10 ])
	];
}
namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitIfStatement - constant condition branch elimination
/// </summary>
[InheritsTests]
public class VisitIfStatementTests : BaseTestWithRandomValues<Func<bool, int, int, (int, int, int, int)>>
{
	public override string TestMethod => GetString((condition, x, y) =>
	{
		int a;
		if (true)
			a = 1;
		else
			a = 2;

		int b;
		if (false)
			b = 3;
		b = 4;

		int c;
		if (condition)
			c = x;
		else
			c = y;

		int d;
		if (x > y)
			d = x;
		else
			d = y;

		return (a, b, c, d);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((condition, x, y) => (1, 4, condition ? x : y, Int32.Max(x, y))),
	];
}
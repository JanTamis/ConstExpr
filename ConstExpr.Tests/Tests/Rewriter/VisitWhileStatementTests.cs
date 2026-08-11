namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitWhileStatement - loop unrolling with constant condition
/// </summary>
[InheritsTests]
public class VisitWhileStatementTests : BaseTestWithRandomValues<Func<int, bool, (int, int, int, int)>>
{
	protected override int MaxRandomMagnitudeBits => 4;

	public override string TestMethod => GetString((limit, condition) =>
	{
		var a = 0;

		while (false)
		{
			a++;
		}

		var b = 10;

		while (true)
		{
			b++;
			break;
		}

		var c = 0;

		while (c < limit)
		{
			c++;
		}

		var d = 5;

		while (condition)
		{
			d--;
			break;
		}

		return (a, b, c, d);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((limit, condition) =>
		{
			var c = 0;

			while (c < limit)
			{
				c++;
			}

			var d = 5;

			if (condition)
			{
				d--;
			}

			return (0, 11, c, d);
		}),
	];
}
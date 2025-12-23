namespace ConstExpr.Tests.Tests.Rewriter;

/// <summary>
/// Tests for VisitWhileStatement - loop unrolling with constant condition
/// </summary>
[InheritsTests]
public class VisitWhileStatementTests : BaseTest<Func<int, bool, (int, int, int, int)>>
{
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
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

			return (0, 11, c, d);
			""", Unknown, Unknown),
		Create("return (0, 11, 3, 4);", 3, true),
		Create("return (0, 11, 0, 4);", 0, true),
		Create("return (0, 11, 5, 5);", 5, false),
		Create("return (0, 11, 10, 4);", 10, true)
	];
}
namespace ConstExpr.Tests.Rewriter;

/// <summary>
///   Tests for VisitWhileStatement - loop unrolling with constant condition
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
		Create((_, _) => (0, 11, 3, 4), [ 3, true ]),
		Create((_, _) => (0, 11, 0, 4), [ 0, true ]),
		Create((_, _) => (0, 11, 5, 5), [ 5, false ]),
		Create((_, _) => (0, 11, 10, 4), [ 10, true ])
	];
}
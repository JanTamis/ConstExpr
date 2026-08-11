namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class FibonacciTest : BaseTestWithRandomValues<Func<int, long>>
{
	protected override int MaxRandomMagnitudeBits => 5;

	public override string TestMethod => GetString(n =>
	{
		if (n <= 0)
		{
			return 0;
		}

		if (n == 1)
		{
			return 1;
		}

		var prev = 0L;
		var curr = 1L;

		for (var i = 2; i <= n; i++)
		{
			var next = prev + curr;
			prev = curr;
			curr = next;
		}

		return curr;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n =>
		{
			switch (n)
			{
				case <= 0:
					return 0L;
				case 1:
					return 1L;
			}

			var prev = 0L;
			var curr = 1L;

			for (var i = 2; i <= n; i++)
			{
				var next = prev + curr;

				prev = curr;
				curr = next;
			}

			return curr;
		}),
	];
}
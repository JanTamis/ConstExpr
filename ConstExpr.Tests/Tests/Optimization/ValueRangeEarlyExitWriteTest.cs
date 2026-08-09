namespace ConstExpr.Tests.Optimization;

/// <summary>
///   The safety check for <see cref="ValueRangeEarlyExitGuardTest" />, and the reason the guards an
///   early exit establishes are tracked per statement rather than for the block as a whole: the
///   assignment between the guard and the use replaced the very value the guard was about. Here
///   <c>n</c> really can be 50 or more by the time the second comparison runs, so it must survive.
/// </summary>
[InheritsTests]
public class ValueRangeEarlyExitWriteTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		if (n < 0 || n > 9)
		{
			return -1;
		}

		n *= 100;

		if (n < 50)
		{
			return n;
		}

		return 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// The second guard is still there — only CSE touched it, hoisting `n * 100` into a local.
		Create(n =>
		{
			if ((uint) n > 9U)
			{
				return -1;
			}

			var prod = n * 100;

			return prod < 50 ? prod : 0;
		}, [ Unknown ]),

		// n = 0 is the one value in range that still passes the second guard; n = 4 is the one that
		// proves it was not folded to true.
		CreateFolded(0),
		CreateFolded(4),
		CreateFolded(40)
	];
}
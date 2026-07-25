namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Guards the one way this pass could do worse than emit clumsy code: folding a loop condition into
///   an infinite loop. The mask pins <c>x</c> to <c>[0, 3]</c> on entry, so <c>x &lt; 10</c> looks
///   settled — but the body advances <c>x</c>, so the entry fact says nothing about later iterations
///   and the condition must survive.
///   <para>
///     Two separate things keep it alive: the write to <c>x</c> inside the loop discards the fact, and
///     a loop's own condition is refused outright. If either is ever loosened this test fails rather
///     than the build hanging.
///   </para>
/// </summary>
[InheritsTests]
public class ValueRangeLoopConditionTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		var x = n & 3;
		var count = 0;

		while (x < 10)
		{
			x += 1;
			count++;
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Unchanged: the condition stays exactly where it was.
		Create(n =>
		{
			var x = n & 3;
			var count = 0;

			while (x < 10)
			{
				x += 1;
				count++;
			}

			return count;
		}, [ Unknown ]),

		// n = 5 masks to 1, so the loop runs from 1 up to 10: nine iterations.
		Create(_ => 9, [ 5 ])
	];
}
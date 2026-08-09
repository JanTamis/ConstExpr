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
///   <para>
///     The same entry fact — <c>x &lt; 10</c> holds for every value the mask can produce — is exactly
///     what <c>OptimizationFlags.WhileToDoWhileConversion</c> is allowed to use: it only changes which
///     loop keyword runs the first check, never the condition expression itself, so the while is
///     expected to come out as a do-while below rather than unchanged.
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
		// The condition itself stays exactly where it was; only while becomes do-while, since x < 10
		// is proven true on the very first check (x starts in [0, 3]).
		Create(n =>
		{
			var x = n & 3;
			var count = 0;

			do
			{
				x += 1;
				count++;
			} while (x < 10);

			return count;
		}, [ Unknown ]),

		// n = 5 masks to 1, so the loop runs from 1 up to 10: nine iterations.
		CreateFolded(5)
	];
}
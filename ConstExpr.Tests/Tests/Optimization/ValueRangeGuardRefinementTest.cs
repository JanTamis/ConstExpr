namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Value-range propagation over an enclosing guard. On the branch the outer <c>if</c> takes,
///   <c>n</c> is known to be in <c>[0, 9]</c> — both halves of the <c>&amp;&amp;</c> contribute an
///   endpoint — so the inner comparison against 50 is settled and disappears.
/// </summary>
[InheritsTests]
public class ValueRangeGuardRefinementTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		if (n >= 0 && n < 10)
		{
			// ReSharper disable once ConditionIsAlwaysTrueOrFalse — being always true is the point.
			if (n < 50)
			{
				return n * 2;
			}
		}

		return 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// The outer guard has already been canonicalised into an unsigned range check and a ternary by
		// the time this pass runs — reading `n` back out of that form is what lets the inner
		// comparison, and the `&& true` it leaves behind, disappear.
		Create(n => (uint) n <= 9U ? n << 1 : 0, [ Unknown ]),

		// Inside the guarded range, and outside it.
		CreateFolded(4),
		CreateFolded(40)
	];
}
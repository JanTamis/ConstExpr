namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Value-range propagation over an <em>early-exit</em> guard, the counterpart to
///   <see cref="ValueRangeGuardRefinementTest" />: the fact does not come from an enclosing <c>if</c>
///   this code sits inside, but from one that already returned. Everything after
///   <c>if (c) return …;</c> is only reached once <c>c</c> did not hold, so the negated condition
///   narrows <c>n</c> just as an enclosing guard would and the second comparison disappears.
///   <para>
///     This is the shape the generator emits for itself — <c>BinomialCoefficient</c> in the Sample
///     used to keep an <c>(uint) k &lt;= 5U</c> that an earlier <c>if ((uint) k > 5U) return 0;</c>
///     had already settled.
///   </para>
/// </summary>
[InheritsTests]
public class ValueRangeEarlyExitGuardTest : BaseTest<Func<int, int>>
{
	public override string TestMethod => GetString(n =>
	{
		if (n < 0 || n > 9)
		{
			return -1;
		}

		// ReSharper disable once ConditionIsAlwaysTrueOrFalse — being always true is the point.
		if (n < 50)
		{
			return n * 2;
		}

		return 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => (uint) n > 9U ? -1 : n << 1, [ Unknown ]),

		// Inside the guarded range, and outside it.
		CreateFolded(4),
		CreateFolded(40)
	];
}
namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Integer +/- isolation across ==, including the task_915264c7 fix: v - c == k isolates to
///   v == k + c, but c - v == k isolates to v == c - k — a different formula, not just a relabeling
///   of the same one, since v has coefficient -1 in the second case. (v + c and c + v are commutative
///   and always isolate identically, so they're covered by a separate single-expression test —
///   combining them here would collide under CSE into a single shared local, obscuring the golden.)
/// </summary>
[InheritsTests]
public class EqualsIntegerAdditiveTest : BaseTest<Func<int, (bool, bool, bool)>>
{
	public override string TestMethod => GetString(x => (x + 3 == 10, x - 3 == 10, 3 - x == 10));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x == 7, x == 13, x == -7)),
		Create(_ => (true, false, false), [ 7 ]),
		Create(_ => (false, true, false), [ 13 ]),
		Create(_ => (false, false, true), [ -7 ])
	];
}
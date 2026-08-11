namespace ConstExpr.Tests.Optimization;

/// <summary>
///   c + v == k isolates to v == k - c, same as v + c == k (addition is commutative — no side bug
///   possible here, unlike subtract). Kept as its own single-expression test rather than combined
///   with EqualsIntegerAdditiveTest's v + c == k case, since both isolate to the identical x == 7 and
///   would collide under CSE into one shared local, obscuring the golden.
/// </summary>
[InheritsTests]
public class EqualsIntegerReverseAddTest : BaseTestWithRandomValues<Func<int, bool>>
{
	public override string TestMethod => GetString(x => 3 + x == 10);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x == 7),
	];
}
namespace ConstExpr.Tests.Optimization;

/// <summary>
///   c / v == k isolates to v == c / k for float/double — the one reciprocal form
///   RelationalVariableIsolationStrategy deliberately excludes for inequalities (monotonicity depends
///   on the sign of v there) but which is safe for equality.
/// </summary>
[InheritsTests]
public class EqualsReciprocalTest : BaseTest<Func<float, (bool, bool)>>
{
	// ReSharper disable CompareOfFloatsByEqualityOperator — testing exactly that comparison.
	public override string TestMethod => GetString(x => (6 / x == 2, 6 / x != 2));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x == 3f, x != 3f)),
		// ReSharper restore CompareOfFloatsByEqualityOperator
		CreateFolded(3f),
		CreateFolded(4f)
	];
}
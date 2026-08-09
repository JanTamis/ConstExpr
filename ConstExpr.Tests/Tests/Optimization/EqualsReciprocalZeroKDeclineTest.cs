namespace ConstExpr.Tests.Optimization;

/// <summary>
///   c / v == 0 is declined (not isolated): for c != 0 there is no finite v satisfying it (as v grows,
///   c / v approaches but never reaches 0), so there is no single threshold to fold to.
/// </summary>
[InheritsTests]
public class EqualsReciprocalZeroKDeclineTest : BaseTest<Func<float, bool>>
{
	// ReSharper disable CompareOfFloatsByEqualityOperator — testing exactly that comparison.
	public override string TestMethod => GetString(x => 6 / x == 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => 6F / x == 0F),
		// ReSharper restore CompareOfFloatsByEqualityOperator
		CreateFolded(3f),
		CreateFolded(1000000f)
	];
}
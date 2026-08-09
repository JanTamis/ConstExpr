namespace ConstExpr.Tests.Optimization;

/// <summary>
///   A compile-time coefficient multiplied onto a comparison's variable operand is isolated by
///   dividing it across the comparison: v * c OP k => v OP k / c. Uses c = 4 so the isolated
///   threshold (0.25) is exactly representable in float, keeping the expected literal unambiguous.
/// </summary>
[InheritsTests]
public class ComparisonCoefficientDivisionTest : BaseTest<Func<float, (bool, bool, bool, bool)>>
{
	public override string TestMethod => GetString(x => (x * 4 < 1, x * 4 > 1, x * 4 <= 1, x * 4 >= 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => (x < 0.25f, x > 0.25f, x <= 0.25f, x >= 0.25f)),
		CreateFolded(0f),
		CreateFolded(1f),
		CreateFolded(0.25f)
	];
}
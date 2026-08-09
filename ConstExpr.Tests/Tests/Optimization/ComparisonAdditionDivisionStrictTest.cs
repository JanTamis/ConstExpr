using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Without FastMathFlags.AssociativeMath, neither the additive form (v + c) nor the v + v (c = 2)
///   equality form isolates — same reasoning as ComparisonCoefficientDivisionStrictTest, extended to
///   the two new code paths this session added.
///   <para>
///     Deliberately not testing <c>x * 6 == 1</c> here: that shape is already isolated under Strict
///     by the pre-existing, ungated <c>EqualsComparisonSimplifierStrategy</c>, so it wouldn't test
///     this strategy's own gating. <c>x + x == 1</c> has no literal operand for that strategy to key
///     on, so this new strategy is the only thing that could isolate it.
///   </para>
/// </summary>
[InheritsTests]
public class ComparisonAdditionDivisionStrictTest() : BaseTest<Func<float, (bool, bool)>>(FastMathFlags.Strict)
{
	// ReSharper disable once CompareOfFloatsByEqualityOperator — testing exactly that comparison.
	public override string TestMethod => GetString(x => (x + 3 < 1, x + x == 1));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// ReSharper disable once CompareOfFloatsByEqualityOperator
		Create(x => (x + 3F < 1F, x + x == 1F)),
		CreateFolded(0f),
		CreateFolded(-5f)
	];
}
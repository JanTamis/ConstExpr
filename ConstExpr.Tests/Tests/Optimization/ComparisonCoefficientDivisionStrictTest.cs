using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Without FastMathFlags.AssociativeMath the coefficient stays multiplied in — isolating it via
///   division is not exact for float/double (k / c can round differently than the source's
///   multiply-then-compare) and must not happen under Strict.
/// </summary>
[InheritsTests]
public class ComparisonCoefficientDivisionStrictTest() : BaseTestWithRandomValues<Func<float, bool>>(FastMathFlags.Strict)
{
	public override string TestMethod => GetString(x => x * 6 < 1);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => x * 6F < 1F),
	];
}
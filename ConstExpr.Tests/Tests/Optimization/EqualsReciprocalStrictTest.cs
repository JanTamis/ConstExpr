using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Optimization;

/// <summary>
///   Without FastMathFlags.AssociativeMath, c / v == k stays as-is — isolating via c / k is not
///   guaranteed bit-exact for float/double.
/// </summary>
[InheritsTests]
public class EqualsReciprocalStrictTest() : BaseTest<Func<float, bool>>(FastMathFlags.Strict)
{
	// ReSharper disable CompareOfFloatsByEqualityOperator — testing exactly that comparison.
	public override string TestMethod => GetString(x => 6 / x == 2);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => 6F / x == 2F),
		// ReSharper restore CompareOfFloatsByEqualityOperator
		Create(_ => true, [ 3f ]),
		Create(_ => false, [ 4f ])
	];
}
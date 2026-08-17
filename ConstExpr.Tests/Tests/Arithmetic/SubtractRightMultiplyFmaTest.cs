using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

// Guards SubtractFMARightMultiplyStrategy: x - y * x => MultiplyAddEstimate(-y, x, x). Must stay gated behind
// FastMathFlags.FusedMultiplyAdd (different rounding behaviour), mirroring SubtractFMALeftMultiplyStrategy.
[InheritsTests]
public class SubtractRightMultiplyFmaTest : BaseTest<Func<double, double, double>>
{
	public override string TestMethod => GetString((x, y) => x - y * x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("return Double.MultiplyAddEstimate(-y, x, x);", Unknown, Unknown)
	];
}

[InheritsTests]
public class SubtractRightMultiplyFmaStrictTest() : BaseTest<Func<double, double, double>>(FastMathFlags.Strict)
{
	public override string TestMethod => GetString((x, y) => x - y * x);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null, Unknown, Unknown)
	];
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NegateAdditionUintGateTest() : BaseTest<Func<uint, long>>(FastMathFlags.NoSignedZero)
{
	public override string TestMethod => GetString(n => -(5 + n));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		// Rewrite must NOT fire for unsigned: only the commutative reorder remains, never -5 - n.
		Create(n => -(n + 5U))
	];
}
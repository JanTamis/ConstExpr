using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Rewriter;

[InheritsTests]
public class NegateAdditionDoubleTest() : BaseTest<Func<double, double>>(FastMathFlags.NoSignedZero)
{
	public override string TestMethod => GetString(f => -(5D + f));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(f => -5D - f),
		Create(_ => -15D, [ 10D ]),
		Create(_ => -5D, [ 0D ])
	];
}
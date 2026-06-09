using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

/// <summary>Tests for Pow algebraic strategies: literal base transformations.</summary>
[InheritsTests]
public class PowTwoToExpTest() : BaseTest<Func<double, double>>(FastMathFlags.All)
{
	public override string TestMethod => GetString(n => System.Math.Pow(2.0, n));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => double.Exp2(n)),
		Create(_ => 8D, [ 3.0 ]),
		Create(_ => 1D, [ 0.0 ])
	];
}

[InheritsTests]
public class PowTenToExpTest() : BaseTest<Func<double, double>>(FastMathFlags.All)
{
	public override string TestMethod => GetString(n => System.Math.Pow(10.0, n));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => double.Exp10(n)),
		Create(_ => 1000D, [ 3.0 ]),
		Create(_ => 1D, [ 0.0 ])
	];
}

[InheritsTests]
public class PowNegHalfExpTest() : BaseTest<Func<double, double>>(FastMathFlags.All)
{
	public override string TestMethod => GetString(x => System.Math.Pow(x, -0.5));

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(x => Double.ReciprocalSqrtEstimate(x)),
		Create(_ => 0.5D, [ 4.0 ]),
		Create(_ => 1D, [ 1.0 ])
	];
}
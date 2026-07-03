using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsDivisibleByTest() : BaseTest<Func<int, int, bool>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString((n, divisor) => divisor != 0 && n % divisor == 0);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create((_, _) => true, [ 10, 5 ]),
		Create((_, _) => false, [ 10, 3 ]),
		Create((_, _) => false, [ 0, 0 ])
	];
}
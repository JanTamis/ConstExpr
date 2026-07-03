using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumOfSquaresTest() : BaseTest<Func<int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(n =>
	{
		if (n <= 0)
		{
			return 0;
		}

		var total = 0;

		for (var i = 1; i <= n; i++)
		{
			total += i * i;
		}

		return total;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => 55, [ 5 ]),
		Create(_ => 0, [ 0 ]),
		Create(_ => 14, [ 3 ])
	];
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class SignTest() : BaseTest<Func<int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(n =>
	{
		if (n > 0)
		{
			return 1;
		}

		if (n < 0)
		{
			return -1;
		}

		return 0;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => n > 0 ? 1 : n < 0 ? -1 : 0),
		Create(_ => 1, [ 100 ]),
		Create(_ => -1, [ -50 ]),
		Create(_ => 0, [ 0 ])
	];
}
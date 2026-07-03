using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class AbsoluteValueTest() : BaseTest<Func<int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(n =>
	{
		if (n < 0)
		{
			return -n;
		}

		return n;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(n => Int32.Abs(n)),
		Create(_ => 42, [ -42 ]),
		Create(_ => 10, [ 10 ]),
		Create(_ => 0, [ 0 ])
	];
}
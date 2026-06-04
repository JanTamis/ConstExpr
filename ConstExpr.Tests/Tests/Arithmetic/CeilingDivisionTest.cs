using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class CeilingDivisionTest() : BaseTest<Func<int, int, int>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((numerator, divisor) =>
	{
		if (divisor == 0)
		{
			return 0;
		}

		return (numerator + divisor - 1) / divisor;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((numerator, divisor) => divisor == 0 ? 0 : (numerator + divisor - 1) / divisor),
		Create((_, _) => 3, [ 10, 4 ]),
		Create((_, _) => 5, [ 20, 4 ]),
		Create((_, _) => 0, [ 10, 0 ]),
		Create((numerator, _) => (numerator + 4) / 5, [ Unknown, 5 ]),
		Create((_, _) => 0, [ Unknown, 0 ])
	];
}
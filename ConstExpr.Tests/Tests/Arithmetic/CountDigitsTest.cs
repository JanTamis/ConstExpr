using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class CountDigitsTest() : BaseTest<Func<int, int>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n =>
	{
		if (n == 0)
		{
			return 1;
		}

		if (n < 0)
		{
			n = -n;
		}

		var count = 0;

		while (n > 0)
		{
			count++;
			n /= 10;
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create(_ => 3, [ 123 ]),
		Create(_ => 1, [ 0 ]),
		Create(_ => 4, [ -4567 ])
	];
}
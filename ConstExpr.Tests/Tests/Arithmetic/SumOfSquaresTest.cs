using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumOfSquaresTest() : BaseTest<Func<int, int>>(FastMathFlags.FastMath)
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
		Create(null),
		Create("return 55;", 5),
		Create("return 0;", 0),
		Create("return 14;", 3)
	];
}
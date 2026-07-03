using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class DigitalRootTest() : BaseTest<Func<int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(n =>
	{
		var num = System.Math.Abs(n);

		while (num >= 10)
		{
			var sum = 0;

			while (num > 0)
			{
				sum += num % 10;
				num /= 10;
			}

			num = sum;
		}

		return num;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var num = FastAbs(n);
			var sum = 0;

			while (num >= 10)
			{
				while (num > 0)
				{
					sum += num % 10;
					num /= 10;
				}

				num = sum;
			}

			return num;
			"""),
		Create(_ => 2, [ 38 ]),
		Create(_ => 6, [ 942 ]),
		Create(_ => 0, [ 0 ])
	];
}
using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class PowerTest() : BaseTest<Func<int, int, long>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((baseNum, exponent) =>
	{
		if (exponent < 0)
		{
			return 0L;
		}

		if (exponent == 0)
		{
			return 1L;
		}

		var result = 1L;
		var base64 = (long) baseNum;

		while (exponent > 0)
		{
			if (exponent % 2 == 1)
			{
				result *= base64;
			}

			base64 *= base64;
			exponent /= 2;
		}

		return result;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((baseNum, exponent) =>
		{
			if (exponent < 0)
			{
				return 0L;
			}

			if (exponent == 0)
			{
				return 1L;
			}

			var result = 1L;
			var base64 = (long) baseNum;

			while (exponent > 0)
			{
				if (Int32.IsOddInteger(exponent))
				{
					result *= base64;
				}

				base64 *= base64;
				exponent >>= 1;
			}

			return result;
		}),
		Create((_, _) => 32L, [ 2, 5 ]),
		Create((_, _) => 1L, [ 5, 0 ]),
		Create((_, _) => 0L, [ 2, -3 ]),
		Create((_, _) => 1024L, [ 2, 10 ])
	];
}
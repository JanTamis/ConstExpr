using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class PowerTest() : BaseTest<Func<int, int, long>>(FloatingPointEvaluationMode.FastMath)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			if (exponent < 0)
			{
				return 0L;
			}

			if (exponent == 0)
			{
				return 1L;
			}

			var result = 1L;
			var base64 = (long)baseNum;

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
			""", Unknown, Unknown),
		Create("return 32L;", 2, 5),
		Create("return 1L;", 5, 0),
		Create("return 0L;", 2, -3),
		Create("return 1024L;", 2, 10)
	];
}
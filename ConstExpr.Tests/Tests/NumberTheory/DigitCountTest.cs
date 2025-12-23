using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class DigitCountTest() : BaseTest<Func<int, int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(n =>
	{
		if (n == 0)
		{
			return 1;
		}

		var count = 0;
		var num = System.Math.Abs(n);

		while (num > 0)
		{
			count++;
			num /= 10;
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			if (n == 0)
			{
				return 1;
			}
			
			var count = 0;
			var num = Int32.Abs(n);
			
			while (num > 0)
			{
				count++;
				num /= 10;
			}
			
			return count;
			""", Unknown),
		Create("return 3;", 123),
		Create("return 1;", 0),
		Create("return 4;", -1234)
	];
}
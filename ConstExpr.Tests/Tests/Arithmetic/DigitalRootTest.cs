using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class DigitalRootTest() : BaseTest<Func<int, int>>(FastMathFlags.FastMath)
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
			var num = AbsFast(n);
			
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
			""", Unknown),
		Create("return 2;", 38),
		Create("return 6;", 942),
		Create("return 0;", 0)
	];
}
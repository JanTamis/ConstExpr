using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class ProductOfDigitsTest () : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
		var product = 1;
		var num = Int32.Abs(n);
		
		while (num > 0)
		{
			product *= num % 10;
			num /= 10;
		}
		
		return product;
		""", Unknown),
		Create("return 24;", 234),
		Create("return 0;", 105),
		Create("return 5;", 5),
	];

	public override string TestMethod => """
		int ProductOfDigits(int n)
		{
			var product = 1;
			var num = Math.Abs(n);
			
			while (num > 0)
			{
				product *= num % 10;
				num /= 10;
			}
			
			return product;
		}
		""";
}


using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class ProductOfDigitsTest() : BaseTest<Func<int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n =>
	{
		var product = 1;
		var num = System.Math.Abs(n);

		while (num > 0)
		{
			product *= num % 10;
			num /= 10;
		}

		return product;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var product = 1;
			var num = AbsFast(n);

			while (num > 0)
			{
				product *= num - (((int)((long)num * 1717986919 >> 32) >> 2) + ((int)((long)num * 1717986919 >> 32) >> 2 >>> 31)) * 10;
				num = ((int)((long)num * 1717986919 >> 32) >> 2) + ((int)((long)num * 1717986919 >> 32) >> 2 >>> 31);
			}

			return product;
			"""),
		Create(_ => 24, [ 234 ]),
		Create(_ => 0, [ 105 ]),
		Create(_ => 5, [ 5 ])
	];
}
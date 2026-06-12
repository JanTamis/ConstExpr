using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class ReverseNumberTest() : BaseTest<Func<int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(n =>
	{
		var originalN = n;
		n = System.Math.Abs(n);

		var reversed = 0;

		while (n > 0)
		{
			reversed = reversed * 10 + n % 10;
			n /= 10;
		}

		return Int32.CopySign(reversed, originalN);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			var originalN = n;

			n = AbsFast(n);

			var reversed = 0;

			while (n > 0)
			{
				reversed = reversed * 10 + n - (((int)((long)n * 1717986919 >> 32) >> 2) + ((int)((long)n * 1717986919 >> 32) >> 2 >>> 31)) * 10;
				n = ((int)((long)n * 1717986919 >> 32) >> 2) + ((int)((long)n * 1717986919 >> 32) >> 2 >>> 31);
			}

			return CopySignFast(reversed, originalN);
			"""),
		Create(_ => 321, [ 123 ]),
		Create(_ => -654, [ -456 ]),
		Create(_ => 1, [ 1 ])
	];
}
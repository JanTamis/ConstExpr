using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class ReverseNumberTest() : BaseTest<Func<int, int>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
{
	public override string TestMethod => GetString(n =>
	{
		var originalN = n;
		var reversed = 0;

		n = System.Math.Abs(n);

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
			var reversed = 0;

			n = FastAbs(n);

			while (n > 0)
			{
				reversed = reversed * 10 + n % 10;
				n /= 10;
			}

			return FastCopySign(reversed, originalN);
			"""),
		Create(_ => 321, [ 123 ]),
		Create(_ => -654, [ -456 ]),
		Create(_ => 1, [ 1 ])
	];
}
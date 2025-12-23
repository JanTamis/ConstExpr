using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class ReverseNumberTest() : BaseTest<Func<int, int>>(FloatingPointEvaluationMode.FastMath)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			var originalN = n;

			n = Int32.Abs(n);

			var reversed = 0;

			while (n > 0)
			{
				reversed = reversed * 10 + n % 10;
				n /= 10;
			}

			return Int32.CopySign(reversed, originalN);
			""", Unknown),
		Create("return 321;", 123),
		Create("return -654;", -456),
		Create("return 1;", 1)
	];
}
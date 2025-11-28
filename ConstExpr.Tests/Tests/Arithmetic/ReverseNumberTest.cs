namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class ReverseNumberTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 321;", 123),
		Create("return -654;", -456),
		Create("return 1;", 1),
	];

	public override string TestMethod => """
		int ReverseNumber(int n)
		{
			var originalN = n;
			n = Math.Abs(n);

			var reversed = 0;
			while (n > 0)
			{
				reversed = reversed * 10 + n % 10;
				n /= 10;
			}

			return Int32.CopySign(reversed, originalN);
		}
		""";
}

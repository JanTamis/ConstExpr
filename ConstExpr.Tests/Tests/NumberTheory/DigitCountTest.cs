namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class DigitCountTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 3;", 123),
		Create("return 1;", 0),
		Create("return 4;", -1234),
	];

	public override string TestMethod => """
		int DigitCount(int n)
		{
			if (n == 0)
			{
				return 1;
			}
			
			var count = 0;
			var num = Math.Abs(n);
			
			while (num > 0)
			{
				count++;
				num /= 10;
			}
			
			return count;
		}
		""";
}


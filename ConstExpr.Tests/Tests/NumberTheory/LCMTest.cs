namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class LCMTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 12;", 4, 6),
		Create("return 0;", 0, 5),
		Create("return 42;", 21, 6),
	];

	public override string TestMethod => """
		int LCM(int a, int b)
		{
			if (a == 0 || b == 0)
			{
				return 0;
			}
			var aa = Math.Abs(a);
			var bb = Math.Abs(b);
			while (bb != 0)
			{
				var temp = bb;
				bb = aa % bb;
				aa = temp;
			}
			var gcd = aa;
			return Math.Abs(a * b) / gcd;
		}
		""";
}


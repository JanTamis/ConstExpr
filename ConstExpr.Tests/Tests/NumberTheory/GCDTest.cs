namespace ConstExpr.Tests.NumberTheory;

[InheritsTests]
public class GCDTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 6;", 48, 18),
		Create("return 1;", 17, 19),
		Create("return 15;", 45, 60),
	];

	public override string TestMethod => """
		int GCD(int a, int b)
		{
			a = Math.Abs(a);
			b = Math.Abs(b);

			while (b != 0)
			{
				var temp = b;
				b = a % b;
				a = temp;
			}

			return a;
		}
		""";
}

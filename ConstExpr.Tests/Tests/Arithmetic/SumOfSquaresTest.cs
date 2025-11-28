namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumOfSquaresTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 55;", 5),
		Create("return 0;", 0),
		Create("return 14;", 3),
	];

	public override string TestMethod => """
		int SumOfSquares(int n)
		{
			if (n <= 0)
			{
				return 0;
			}
			var total = 0;
			for (var i = 1; i <= n; i++)
			{
				total += i * i;
			}
			return total;
		}
		""";
}


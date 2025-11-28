namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumRangeTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 55L;", 1, 10),
		Create("return 5050L;", 1, 100),
		Create("return 25L;", 3, 7),
	];

	public override string TestMethod => """
		long SumRange(int start, int end)
		{
			if (start > end)
			{
				var temp = start;
				start = end;
				end = temp;
			}

			var n = end - start + 1;
			return (long)n * (start + end) / 2;
		}
		""";
}

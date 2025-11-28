namespace ConstExpr.Tests.Array;

[InheritsTests]
public class CountEvensTest : BaseTest
{
	public override IEnumerable<KeyValuePair<string?, object[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 3;", new[] { 1, 2, 3, 4, 5, 6 }),
		Create("return 0;", System.Array.Empty<int>()),
		Create("return 4;", new[] { 2, 4, 6, 8 }),
	];

	public override string TestMethod => """
		int CountEvens(int[] arr)
		{
			var count = 0;
			foreach (var num in arr)
			{
				if (num % 2 == 0)
				{
					count++;
				}
			}
			return count;
		}
		""";
}


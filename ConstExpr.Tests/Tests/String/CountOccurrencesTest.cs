namespace ConstExpr.Tests.String;

[InheritsTests]
public class CountOccurrencesTest : BaseTest<Func<int, int[], int>>
{
	public override string TestMethod => GetString((target, numbers) =>
	{
		var count = 0;

		foreach (var num in numbers)
		{
			if (num == target)
			{
				count++;
			}
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(5, new[] { 5, 5, 10, 5, 20, 5 }),
		CreateFolded(100, new[] { 1, 2, 3, 4, 5 }),
		CreateFolded(7, new[] { 7, 14, 21, 7 })
	];
}
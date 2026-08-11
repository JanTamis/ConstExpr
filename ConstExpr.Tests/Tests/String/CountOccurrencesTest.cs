namespace ConstExpr.Tests.String;

[InheritsTests]
public class CountOccurrencesTest : BaseTestWithRandomValues<Func<int, int[], int>>
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
	];
}
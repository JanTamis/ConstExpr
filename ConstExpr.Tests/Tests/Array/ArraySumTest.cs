namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArraySumTest : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(arr =>
	{
		var sum = 0;

		foreach (var num in arr)
		{
			sum += num;
		}

		return sum;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(new[] { 1, 2, 3, 4, 5 }),
		CreateFolded(System.Array.Empty<int>()),
		CreateFolded(new[] { 42 })
	];
}
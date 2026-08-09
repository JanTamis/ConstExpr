namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArrayProductTest : BaseTest<Func<int[], int>>
{
	public override string TestMethod => GetString(arr =>
	{
		var product = 1;

		foreach (var num in arr)
		{
			product *= num;
		}

		return product;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(new[] { 1, 2, 3, 4, 5 }),
		CreateFolded(System.Array.Empty<int>()),
		CreateFolded(new[] { 5, 0, 3 })
	];
}
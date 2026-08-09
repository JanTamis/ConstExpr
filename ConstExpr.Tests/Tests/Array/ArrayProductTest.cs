namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArrayProductTest : BaseTestWithRandomValues<Func<int[], int>>
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
	];
}
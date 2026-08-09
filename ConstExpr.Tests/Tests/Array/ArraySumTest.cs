namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArraySumTest : BaseTestWithRandomValues<Func<int[], int>>
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
	];
}
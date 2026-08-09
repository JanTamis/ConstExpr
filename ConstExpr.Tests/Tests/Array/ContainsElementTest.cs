namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ContainsElementTest : BaseTest<Func<int[], int, bool>>
{
	public override string TestMethod => GetString((arr, value) =>
	{
		foreach (var item in arr)
		{
			if (item == value)
			{
				return true;
			}
		}

		return false;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		CreateFolded(new[] { 1, 2, 3, 4, 5 }, 3),
		CreateFolded(new[] { 10, 20, 30 }, 5)
	];
}
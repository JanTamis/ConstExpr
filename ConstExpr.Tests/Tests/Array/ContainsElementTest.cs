namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ContainsElementTest : BaseTestWithRandomValues<Func<int[], int, bool>>
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
	];
}
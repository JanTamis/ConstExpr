namespace ConstExpr.Tests.Array;

[InheritsTests]
public class CountEvensTest : BaseTestWithRandomValues<Func<int[], int>>
{
	public override string TestMethod => GetString(arr =>
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
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(arr =>
		{
			var count = 0;

			foreach (var num in arr)
			{
				if (Int32.IsEvenInteger(num))
					count++;
			}

			return count;
		}),
	];
}
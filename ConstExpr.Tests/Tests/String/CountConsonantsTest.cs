namespace ConstExpr.Tests.String;

[InheritsTests]
public class CountConsonantsTest : BaseTest<Func<string, int>>
{
	public override string TestMethod => GetString(input =>
	{
		if (string.IsNullOrEmpty(input))
		{
			return 0;
		}

		var count = 0;

		foreach (var c in input)
		{
			if (char.IsLetter(c))
			{
				var lower = char.ToLower(c);

				if (lower != 'a' && lower != 'e' && lower != 'i' && lower != 'o' && lower != 'u')
				{
					count++;
				}
			}
		}
		
		return count;
	});
	
	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
	];
}
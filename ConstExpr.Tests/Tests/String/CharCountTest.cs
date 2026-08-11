namespace ConstExpr.Tests.String;

[InheritsTests]
public class CharCountTest : BaseTestWithRandomValues<Func<string?, char, int>>
{
	public override string TestMethod => GetString((text, target) =>
	{
		if (text is null || text.Length == 0)
		{
			return 0;
		}

		var count = 0;

		foreach (var c in text)
		{
			if (c == target)
			{
				count++;
			}
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create("""
			if (String.IsNullOrEmpty(text))
				return 0;

			var count = 0;

			foreach (var c in text)
			{
				if (c == target)
					count++;
			}

			return count;
			"""),
	];
}
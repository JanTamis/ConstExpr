using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class CharCountTest() : BaseTest<Func<string?, char, int>>(FloatingPointEvaluationMode.FastMath)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create("""
			if (String.IsNullOrEmpty(text))
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
			""", Unknown, Unknown),
		Create("return 3;", "ababa", 'a'),
		Create("return 2;", "aaXXa", 'X'),
		Create("return 0;", "", 'a')
	];
}
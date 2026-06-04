using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class CharCountTest() : BaseTest<Func<string?, char, int>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
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
			"""),
		Create((_, _) => 3, [ "ababa", 'a' ]),
		Create((_, _) => 2, [ "aaXXa", 'X' ]),
		Create((_, _) => 0, [ "", 'a' ])
	];
}
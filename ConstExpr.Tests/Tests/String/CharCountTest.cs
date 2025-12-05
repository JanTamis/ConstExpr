using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class CharCountTest() : BaseTest(FloatingPointEvaluationMode.FastMath)
{
	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 3;", "ababa", 'a'),
		Create("return 2;", "aaXXa", 'X'),
		Create("return 0;", "", 'a'),
	];

	public override string TestMethod => """
		int CharCount(string text, char target)
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
		}
		""";
}


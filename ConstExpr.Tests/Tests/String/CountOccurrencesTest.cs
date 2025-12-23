using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.String;

[InheritsTests]
public class CountOccurrencesTest() : BaseTest<Func<int, int[], int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString((target, numbers) =>
	{
		var count = 0;

		foreach (var num in numbers)
		{
			if (num == target)
			{
				count++;
			}
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown, Unknown),
		Create("return 4;", 5, new[] { 5, 5, 10, 5, 20, 5 }),
		Create("return 0;", 100, new[] { 1, 2, 3, 4, 5 }),
		Create("return 2;", 7, new[] { 7, 14, 21, 7 })
	];
}
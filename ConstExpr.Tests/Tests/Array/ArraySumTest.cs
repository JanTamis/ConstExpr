using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArraySumTest() : BaseTest<Func<int[], int>>(FloatingPointEvaluationMode.FastMath)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 15;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 0;", System.Array.Empty<int>()),
		Create("return 42;", new[] { 42 })
	];
}
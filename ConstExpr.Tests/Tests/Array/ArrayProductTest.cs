using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArrayProductTest() : BaseTest<Func<int[], int>>(FloatingPointEvaluationMode.FastMath)
{
	public override string TestMethod => GetString(arr =>
	{
		var product = 1;

		foreach (var num in arr)
		{
			product *= num;
		}

		return product;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> Result =>
	[
		Create(null, Unknown),
		Create("return 120;", new[] { 1, 2, 3, 4, 5 }),
		Create("return 1;", System.Array.Empty<int>()),
		Create("return 0;", new[] { 5, 0, 3 })
	];
}
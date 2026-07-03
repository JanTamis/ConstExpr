using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArraySumTest() : BaseTest<Func<int[], int>>(FastMathFlags.All, optimizations: OptimizationFlags.All)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		CreateDefault(),
		Create(_ => 15, [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => 0, [ System.Array.Empty<int>() ]),
		Create(_ => 42, [ new[] { 42 } ])
	];
}
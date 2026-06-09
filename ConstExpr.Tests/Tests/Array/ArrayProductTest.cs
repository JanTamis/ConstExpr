using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ArrayProductTest() : BaseTest<Func<int[], int>>(FastMathFlags.All, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
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

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create(_ => 120, [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => 1, [ System.Array.Empty<int>() ]),
		Create(_ => 0, [ new[] { 5, 0, 3 } ])
	];
}
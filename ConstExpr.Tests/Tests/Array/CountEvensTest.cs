using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class CountEvensTest() : BaseTest<Func<int[], int>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(arr =>
	{
		var count = 0;

		foreach (var num in arr)
		{
			if (num % 2 == 0)
			{
				count++;
			}
		}

		return count;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(arr =>
		{
			var count = 0;

			foreach (var num in arr)
			{
				if (Int32.IsEvenInteger(num))
				{
					count++;
				}
			}

			return count;
		}),
		Create(_ => 3, [ new[] { 1, 2, 3, 4, 5, 6 } ]),
		Create(_ => 0, [ System.Array.Empty<int>() ]),
		Create(_ => 4, [ new[] { 2, 4, 6, 8 } ])
	];
}
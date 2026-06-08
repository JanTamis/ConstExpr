using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class IsSortedTest() : BaseTest<Func<int[], bool>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString(numbers =>
	{
		if (numbers.Length <= 1)
		{
			return true;
		}

		for (var i = 1; i < numbers.Length; i++)
		{
			if (numbers[i] < numbers[i - 1])
			{
				return false;
			}
		}

		return true;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create(_ => true, [ new[] { 1, 2, 3, 4, 5 } ]),
		Create(_ => false, [ new[] { 5, 3, 1, 2 } ]),
		Create(_ => true, [ new[] { 10, 20, 30 } ])
	];
}
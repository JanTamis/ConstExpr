using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Array;

[InheritsTests]
public class ContainsElementTest() : BaseTest<Func<int[], int, bool>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((arr, value) =>
	{
		foreach (var item in arr)
		{
			if (item == value)
			{
				return true;
			}
		}

		return false;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create((_, _) => true, [ new[] { 1, 2, 3, 4, 5 }, 3 ]),
		Create((_, _) => false, [ new[] { 10, 20, 30 }, 5 ])
	];
}
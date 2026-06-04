using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class SumRangeTest() : BaseTest<Func<int, int, long>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((start, end) =>
	{
		if (start > end)
		{
			var temp = start;
			start = end;
			end = temp;
		}

		var n = end - start + 1;
		return (long) n * (start + end) / 2L;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((start, end) =>
		{
			if (start > end)
			{
				var temp = start;

				start = end;
				end = temp;
			}

			return (long) (end - start + 1) * (start + end) / 2L;
		}),
		Create((_, _) => 55L, [ 1, 10 ]),
		Create((_, _) => 5050L, [ 1, 100 ]),
		Create((_, _) => 25L, [ 3, 7 ])
	];
}
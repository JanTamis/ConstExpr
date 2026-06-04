using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Math;

[InheritsTests]
public class SwapTest() : BaseTest<Func<int, int, (int, int)>>(FastMathFlags.FastMath | FastMathFlags.CommonSubexpressionElimination | FastMathFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) =>
	{
		var temp = a;
		a = b;
		b = temp;

		return (a, b);
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create((_, _) => (20, 10), [ 10, 20 ]),
		Create((_, _) => (0, 42), [ 42, 0 ]),
		Create((_, _) => (-5, 5), [ 5, -5 ])
	];
}
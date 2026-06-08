using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Arithmetic;

[InheritsTests]
public class AbsoluteDifferenceTest() : BaseTest<Func<int, int, int>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((a, b) =>
	{
		var diff = a - b;

		return diff < 0 ? -diff : diff;
	});

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create((a, b) =>
		{
			var diff = a - b;

			return Int32.Abs(diff);
		}),
		Create((_, _) => 5, [ 10, 5 ]),
		Create((_, _) => 30, [ -10, 20 ]),
		Create((_, _) => 0, [ 42, 42 ])
	];
}
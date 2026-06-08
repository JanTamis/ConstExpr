using ConstExpr.Core.Enumerators;

namespace ConstExpr.Tests.Validation;

[InheritsTests]
public class IsInRangeDoubleTest() : BaseTest<Func<double, double, double, bool>>(FastMathFlags.FastMath, optimizations: OptimizationFlags.CommonSubexpressionElimination | OptimizationFlags.TailRecursionElimination)
{
	public override string TestMethod => GetString((value, min, max) => value >= min && value <= max);

	public override IEnumerable<KeyValuePair<string?, object?[]>> TestCases =>
	[
		Create(null),
		Create((value, _, _) => Double.Abs(value - 5.5) <= 4.5, [ Unknown, 1D, 10D ]),
		Create((_, _, _) => false, [ Unknown, 10D, 1D ]),
		Create((_, _, _) => false, [ Unknown, -1D, -10D ]),
		Create((value, _, _) => Double.Abs(value + 5.5) <= 4.5, [ Unknown, -10D, -1D ]),
		Create((_, _, _) => false, [ 15D, 1D, 10D ]),
		Create((_, _, _) => true, [ 1D, 1D, 10D ])
	];
}